﻿using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Forms;
using System.Threading;
using System.Threading.Tasks;
using System.Drawing;
using KeePassLib;
using KeePassLib.Keys;
using KeePassLib.Serialization;
using System.Linq;
using KeePass.Forms;
using KeePassLib.Security;

namespace WinHelloUnlock
{
    public class Library
    {

        /// <summary>
        /// Convert the composite key to a KeyList class
        /// </summary>
        /// <param name="db">The source database.</param>
        /// <returns>A KeyList object containing the composite key.</returns>
        internal static KeyList GetKeys(PwDatabase db)
        {
            CompositeKey dKey = db.MasterKey;
            var passwordKey = dKey.UserKeys.Where(k => k is KcpPassword).FirstOrDefault() as KcpPassword;
            var kFile = dKey.UserKeys.Where(k => k is KcpKeyFile).FirstOrDefault() as KcpKeyFile;
            var uAccount = dKey.UserKeys.Where(k => k is KcpUserAccount).FirstOrDefault() as KcpUserAccount;
            IEnumerable<IUserKey> kList = dKey.UserKeys;
            int kNumber = kList.Count();
            ProtectedString[] pString = new ProtectedString[kNumber];
            String[] tString = new String[kNumber];
            int i = 0;
            foreach (var uKey in kList)
            {
                switch (uKey.GetType().ToString())
                {
                    case "KeePassLib.Keys.KcpPassword":
                        pString[i] = passwordKey.Password;
                        tString[i] = "KeePassLib.Keys.KcpPassword";
                        break;
                    case "KeePassLib.Keys.KcpKeyFile":
                        pString[i] = new ProtectedString(true, kFile.Path);
                        tString[i] = "KeePassLib.Keys.KcpKeyFile";
                        break;
                    case "KeePassLib.Keys.KcpUserAccount":
                        pString[i] = new ProtectedString(true, "WithUA");
                        tString[i] = "KeePassLib.Keys.KcpUserAccount";
                        break;
                }

                ++i;
            }
            return new KeyList(tString, pString);

        }


        /// <summary>
        /// Convert the KeyList object to a composite key
        /// </summary>
        /// <param name="kList">KeyList containing the composite key information.</param>
        /// <returns>CompositeKey object.</returns>
        internal static CompositeKey ConvertToComposite(KeyList kList)
        {
            if (kList.Pass == null || kList.KeyName == null) return new CompositeKey();
            int kNumber = kList.Pass.Count();
            int i = 0;
            CompositeKey mKey = new CompositeKey();
            for (i = 0; i < kNumber; ++i)
            {
                switch (kList.KeyName[i])
                {
                    case "KeePassLib.Keys.KcpPassword":
                        IUserKey mKeyPass = new KcpPassword(kList.Pass[i].ReadString());
                        mKey.AddUserKey(mKeyPass);
                        break;
                    case "KeePassLib.Keys.KcpKeyFile":
                        IUserKey mKeyFile = new KcpKeyFile(kList.Pass[i].ReadString());
                        mKey.AddUserKey(mKeyFile);
                        break;
                    case "KeePassLib.Keys.KcpUserAccount":
                        IUserKey mKeyUser = new KcpUserAccount();
                        mKey.AddUserKey(mKeyUser);
                        break;
                }
            }
            return mKey;
        }

        /// <summary>
        /// Converts a KeyList object to a properly formatted ProtectedString
        /// </summary>
        /// <param name="keys">KeyList containing the composite key information.</param>
        /// <returns>ProtectedString containing KeyList information.</returns>
        internal static ProtectedString ConvertToPString(KeyList keys)
        {
            string div2 = WinHelloUnlockExt.ProductName + ",";
            string div = WinHelloUnlockExt.ShortProductName + ",";
            if (keys.Pass == null || keys.KeyName == null || keys == null) return null;
            ProtectedString pass = ProtectedString.EmptyEx;
            foreach (ProtectedString ps in keys.Pass)
                pass += ps + div;
            pass = pass.Remove(pass.Length - div.Length, div.Length);
            ProtectedString key = new ProtectedString(true, string.Join(div, keys.KeyName));
            key = key.Insert(key.Length, div2);
            return key + pass;
        }

        /// <summary>
        /// Converts a properly formatted ProtectedString to a KeyList object
        /// </summary>
        /// <param name="keyAndPass">Specially formatted ProtectedString containing key information.</param>
        /// <returns>KeyList based on provided ProtectedString.</returns>
        internal static KeyList ConvertKeyList(ProtectedString keyAndPass)  // Maybe find a way to not use String?
        {
            string div2 = WinHelloUnlockExt.ProductName + ",";
            string div = WinHelloUnlockExt.ShortProductName + ",";
            if (keyAndPass == null) return new KeyList(null, null);
            var keyPass = Split(keyAndPass,div2);
            if (keyPass[1] == null) return new KeyList(null, null);
            var keyName = Split(keyPass[0],div);
            var pass = Split(keyPass[1],div);
            string[] keyArray = keyName.Select(ps => ps.ReadString()).ToArray();
            return new KeyList(keyArray, pass.ToArray());
        }

        /// <summary>
        /// Splits a ProtectedString into a ProtectedString list using a string separator
        /// </summary>
        /// <param name="ps">ProtectedString to separate.</param>
        /// <param name="separator"> String separator.</param>
        /// <returns>ProtectedString List.</returns>
        internal static List<ProtectedString> Split(ProtectedString ps, string separator)
        {
            int index = ps.ReadString().IndexOf(separator); // Would this be safe?
            var list = new List<ProtectedString>();
            if (index < 0)
            {
                list.Add(ps);
                return list;
            }
            
            do {
                list.Add(ps.Remove(index, ps.Length - index));
                index += separator.Length;
                ps = ps.Remove(0, index);
                index = ps.ReadString().IndexOf(separator);
            } while (index > 0);
            list.Add(ps);
            return list;
        }

        /// <summary>
        /// Sets the composite key into the KeyPromptForm
        /// </summary>
        /// <param name="keyPromptForm">KeyPromptForm to set the key into.</param>
        /// <param name="compositeKey">CompositeKey Object.</param>
        internal static void SetCompositeKey(KeyPromptForm keyPromptForm, CompositeKey compositeKey)
        {
            var fieldInfo = keyPromptForm.GetType().GetField("m_pKey", BindingFlags.Instance | BindingFlags.NonPublic);
            if (fieldInfo != null)
                fieldInfo.SetValue(keyPromptForm, compositeKey);
        }

        /// <summary>
        /// Closes the KeyPromptForm with a specific result (Equivalent to click the specific button)
        /// </summary>
        /// <param name="keyPromptForm">KeyPromptForm to close.</param>
        /// <param name="result">Result to close the form with.</param>
        internal static void CloseFormWithResult(KeyPromptForm keyPromptForm, DialogResult result)
        {
            // Remove flushing
            keyPromptForm.Visible = false;
            keyPromptForm.Opacity = 0;

            keyPromptForm.DialogResult = result;
            keyPromptForm.Close();
        }

        /// <summary>
        /// Unlocks the database prompted to unlock on the KeyPromptForm
        /// </summary>
        /// <param name="ioInfo">IOConnectionInfo that represents the database.</param>
        /// <param name="keyPromptForm">KeyPromptForm to unlock the database from.</param>
        /// <param name="secureDesktopChanged">Bool that represents if secure desktop had been changed by the plugin.</param>
        internal static void UnlockDatabase(IOConnectionInfo ioInfo, string dbName, KeyPromptForm keyPromptForm, bool secureDesktopChanged)
        {
            if (keyPromptForm.SecureDesktopMode && WinHelloUnlockExt.tries < 1)
            {
                UnlockWithSecure(keyPromptForm, ioInfo, secureDesktopChanged);
            }
            else if (WinHelloUnlockExt.tries < 1 && !secureDesktopChanged)
            {
                UWPLibrary.UnlockWithoutSecure(dbName, keyPromptForm, ioInfo);
            }
        }

        /// <summary>
        /// Handle the database unlock if secure desktop is enabled
        /// </summary>
        /// <param name="ioInfo">IOConnectionInfo that represents the database.</param>
        /// <param name="keyPromptForm">KeyPromptForm to unlock the database from.</param>
        /// <param name="secureDesktop">Bool that represents if secure desktop had been changed by the plugin.</param>
        internal async static void UnlockWithSecure(KeyPromptForm keyPromptForm, IOConnectionInfo ioInfo, bool secureDesktopChanged)
        {
            CloseFormWithResult(keyPromptForm, DialogResult.Cancel);

            await Task.Factory.StartNew(() =>
            {
                KeePass.Program.Config.Security.MasterKeyOnSecureDesktop = false;
                secureDesktopChanged = true;
                Thread.Yield();
                MainForm mainForm = WinHelloUnlockExt.Host.MainWindow;
                Action action = () => mainForm.OpenDatabase(ioInfo, null, false);
                mainForm.Invoke(action);
            })
            .ContinueWith(_ =>
            {
                KeePass.Program.Config.Security.MasterKeyOnSecureDesktop = true;
                secureDesktopChanged = false;
            });
        }

        /// <summary>
		/// Used to modify options form when it loada.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
        internal static void AddWinHelloOptions(OptionsForm optionsForm)
        {
            if (optionsForm.Controls.Find("m_tabMain", true).FirstOrDefault() is TabControl m_tabMain)
            {
                if (m_tabMain.ImageList == null)
                {
                    m_tabMain.ImageList = new ImageList();
                }
                var imageIndex = m_tabMain.ImageList.Images.Add(Properties.Resources.windows_hello16x16, Color.Transparent);

                var newTab = new TabPage(WinHelloUnlockExt.ProductName)
                {
                    UseVisualStyleBackColor = true,
                    ImageIndex = imageIndex
                };

                var optionsPanel = new OptionsPanel();
                newTab.Controls.Add(optionsPanel);
                optionsPanel.Dock = DockStyle.Fill;


                m_tabMain.TabPages.Add(newTab);
            }
        }

    }

    public class KeyList
    {
        private readonly string[] _kName;
        private readonly ProtectedString[] _pName;
        public string[] KeyName
        {
            get { return _kName; }
        }
        public ProtectedString[] Pass
        {
            get { return _pName; }
        }
        public KeyList(string[] e, ProtectedString[] p)
        {
            _kName = e;
            _pName = p;
        }

    }

}