﻿using dlech.PageantSharp;
using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Security.Cryptography;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using NUnit.Framework;
using PageantSharpNUnitTest.Properties;

namespace PageantSharpTest
{

  /// <summary>
  ///This is a test class for PageantWindowTest and is intended
  ///to contain all PageantWindowTest Unit Tests
  ///</summary>	
  [TestFixture()]
#if __MonoCS__
  [Ignore("Mono")]
#endif
  public class WinPageantTest 
  {

    [DllImport("user32.dll")]
    public static extern IntPtr FindWindow(String sClassName, String sAppName);




    /// <summary>
    ///A test for PageantWindow Constructor
    ///</summary>
    [Test()]
    public void PageantWindowConstructorTest()
    {
      // create new instance
      Agent.CallBacks callbacks = new Agent.CallBacks();
      WinPageant target = new WinPageant(callbacks);

      try {
        // emulate a client to make sure window is there
        IntPtr hwnd = FindWindow("Pageant", "Pageant");
        Assert.AreNotEqual(hwnd, IntPtr.Zero);

        // try starting a second instance, this should cause an exception
        Exception exception = null;
        try {
          WinPageant target2 = new WinPageant(callbacks);
          target2.Dispose();
        } catch (Exception ex) {
          exception = ex;
        }
        Assert.IsInstanceOfType(typeof(PageantException), exception);
      } catch (Exception ex) {
        Assert.Fail(ex.ToString());
      } finally {
        // cleanup first instance
        target.Dispose();
      }
    }

    [Test()]
    public void PageantWindowWndProcTest()
    {
      byte[] data = Resources.ssh2_rsa_no_passphrase_ppk;
      PpkFile.GetPassphraseCallback getPassphrase = null;
      PpkFile.WarnOldFileFormatCallback warnOldFileFormat = delegate() { };
      PpkKey keyFromData = PpkFile.ParseData(data, getPassphrase, warnOldFileFormat);

      List<PpkKey> keyList = new List<PpkKey>();
      keyList.Add(keyFromData);

      Agent.GetSSH2KeyListCallback getSSH2KeysCallback = delegate()
      {
        return keyList;
      };

      Agent.GetSSH2KeyCallback getSSH2KeyCallback = delegate(byte[] reqFingerprint)
      {
        foreach (PpkKey key in keyList) {
          byte[] curFingerprint = OpenSsh.GetFingerprint(key.CipherKeyPair);
          if (curFingerprint.Length == reqFingerprint.Length) {
            for (int i = 0; i < curFingerprint.Length; i++) {
              if (curFingerprint[i] != reqFingerprint[i]) {
                break;
              }
              if (i == curFingerprint.Length - 1) {
                return key;
              }
            }
          }
        }
        return null;
      };

      Agent.AddSSH2KeyCallback addSS2KeyCallback = delegate(PpkKey key)
      {
        return true;
      };

      Agent.CallBacks callbacks = new Agent.CallBacks();
      callbacks.getSSH2KeyList = getSSH2KeysCallback;
      callbacks.getSSH2Key = getSSH2KeyCallback;
      callbacks.addSSH2Key = addSS2KeyCallback;
      WinPageant target = new WinPageant(callbacks);
      MessageBox.Show("Click OK when done");
      target.Dispose();
    }

    [Test()]
    public void TestDsaSig()
    {
      DSA dsa1 = DSA.Create();
      DSAParameters dsa1params = dsa1.ExportParameters(true);

      DsaParameters dsa2common = new DsaParameters(
          new BigInteger(1, dsa1params.P),
          new BigInteger(1, dsa1params.Q),
          new BigInteger(1, dsa1params.G));
      DsaPublicKeyParameters dsa2public = new DsaPublicKeyParameters(
          new BigInteger(1, dsa1params.Y), dsa2common);
      DsaPrivateKeyParameters dsa2private = new DsaPrivateKeyParameters(
          new BigInteger(1, dsa1params.X), dsa2common);

      byte[] data = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };

      SHA1 sha = SHA1.Create();
      sha.ComputeHash(data);
      byte[] dsa1result = dsa1.CreateSignature(sha.Hash);

      ISigner dsa2signer = SignerUtilities.GetSigner("SHA-1withDSA");
      //algName = PpkFile.PublicKeyAlgorithms.ssh_dss;

      dsa2signer.Init(true, dsa2private);
      dsa2signer.BlockUpdate(data, 0, data.Length);
      byte[] dsa2result = dsa2signer.GenerateSignature();

      Assert.IsTrue(dsa1.VerifySignature(sha.Hash, dsa2result));

      dsa2signer.Init(false, dsa2public);
      dsa2signer.BlockUpdate(data, 0, data.Length);

      Assert.IsTrue(dsa2signer.VerifySignature(dsa1result));

    }
  }
}
