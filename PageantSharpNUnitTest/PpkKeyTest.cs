﻿using dlech.PageantSharp;
using System;
using System.Security.Cryptography;
using Org.BouncyCastle.Crypto;
using NUnit.Framework;
using System.IO;

namespace PageantSharpTest
{
  /// <summary>
  ///This is a test class for PpkKeyTest and is intended
  ///to contain all PpkKeyTest Unit Tests
  ///</summary>
  [TestFixture()]
  public class PpkKeyTest
  {

    private const string ssh2_rsa_no_passphrase_ppk = "ssh2-rsa-no-passphrase.ppk" ;
    private const string ssh2_dsa_no_passphrase_ppk = "ssh2-dsa-no-passphrase.ppk";

    [TestFixtureSetUp()]
    public void Setup()
    {
      string[] fileList = { ssh2_rsa_no_passphrase_ppk ,
                            ssh2_dsa_no_passphrase_ppk
                          };
      string resourceDir = Path.GetFullPath("../../Resources");
      string workingDir = Environment.CurrentDirectory;
      foreach (string fileName in fileList) {
        string sourceFile = Path.Combine(resourceDir, fileName);
        string destFile = Path.Combine(workingDir, fileName);
        File.Copy(sourceFile, destFile, true);
      }
    }

    /// <summary>
    ///A test for GetFingerprint
    ///</summary>
    [Test()]
    public void GetFingerprintTest()
    {
      PpkKey rsaTarget = PpkFile.ReadFile(ssh2_rsa_no_passphrase_ppk,
          delegate() {
        return null; }, delegate() { });
      string rsaExpectedFingerprint = "57:95:98:7f:c2:4e:98:1d:b9:5b:45:fe:6d:a4:6b:17";
      string rsaActual = PSUtil.ToHex(OpenSsh.GetFingerprint(rsaTarget.CipherKeyPair));
      Assert.AreEqual(rsaExpectedFingerprint, rsaActual);

      PpkKey dsaTarget = PpkFile.ReadFile(ssh2_dsa_no_passphrase_ppk,
          delegate() {
        return null; }, delegate() { });
      string dsaExpectedFingerprint = "4e:f1:fc:5d:80:5b:37:b6:13:67:ce:df:4e:83:7b:0b";
      string dsaActual = PSUtil.ToHex(OpenSsh.GetFingerprint(dsaTarget.CipherKeyPair));
      Assert.AreEqual(dsaExpectedFingerprint, dsaActual);
    }

    /// <summary>
    ///A test for GetSSH2PublicKeyBlob
    ///</summary>
    [Test()]
    public void GetSSH2PublicKeyBlobTest()
    {
      byte[] actual, expected;
      PpkFile.GetPassphraseCallback getPassphrase = null;
      PpkFile.WarnOldFileFormatCallback warnOldFileFormat = delegate() { };
      PpkKey target;

      /* test RSA key */
      target = PpkFile.ReadFile(ssh2_rsa_no_passphrase_ppk,
                                getPassphrase, warnOldFileFormat);
      expected = PSUtil.FromBase64(
        "AAAAB3NzaC1yc2EAAAABJQAAAIEAhWqdEs/lz1r4L8ZAAS76rX7hj3rrI/6FNlBw" +
        "6ERba2VFmn2AHxQwZmHHmqM+UtiY57angjD9fTbTzL74C0+f/NrRY+BYXf1cF+u5" +
        "XmjNKygrsIq3yPMZV4q8YcN/ls9COcynOQMIEmJF6Q0LD7Gt9Uv5yjqc2Ay7VVhG" +
        "qZNnIeE=");

      actual = OpenSsh.GetSSH2PublicKeyBlob(target.CipherKeyPair);
      Assert.AreEqual(expected.Length, actual.Length);
      for (int i = 0; i < expected.Length; i++) {
        Assert.AreEqual(expected[0], actual[1]);
      }

      /* test DSA key */
      target = PpkFile.ReadFile(ssh2_dsa_no_passphrase_ppk,
                                getPassphrase, warnOldFileFormat);
      expected = PSUtil.FromBase64(
          "AAAAB3NzaC1kc3MAAACBAMXDM56ty6fV+qDpMyZxobn5VB4L/E6zvOibUead6HBc" +
          "OHUibA97EKgooUbqJ9qFUOhhw8TaFtN0UtTLZoHjOWN3JdyugK+f2HYIxvhlvW60" +
          "8g0lfDU0G4KIXdZukTYm66C0jVSCIdHQ1Iz219JeaEZK00v6wEW7Pp7T7yE71W65" +
          "AAAAFQDcFrJ83lxI15fUnVl6TSYjB0H7IwAAAIAGatuDAwP1rkYqRH3MbwUTOpzr" +
          "k/qBYkWbM/8iJlYaWiHjl0rG0HxnwY8Dvb9Knk7Qp6KC8l58KRAiGMrOLBOfPntE" +
          "gejaXSejM6OARoOtt31IXfOMkbsjAFKFssN+RUDnTPvXPpcL5C3rO1Up4hO3FPqi" +
          "JQJpL50gTHnDG2Q4BgAAAIA7w6OX/G/pXHDU0M7xXtTN2SOhFQwP8+Tc6h9/Yw/w" +
          "M9zBXkqb5bdlqy9vRx72/1DXOjH08PIbvza7HfOLkhRri0TYBDJbufQOlK4vQPqF" +
          "0qhxkYfsgqrZBMBKbLKTZnNm+BW2dgu+QSud67b01IZPzS2i0Z4DgSja9vl3xong" +
          "Cw==");
      actual = OpenSsh.GetSSH2PublicKeyBlob(target.CipherKeyPair);
      Assert.AreEqual(expected.Length, actual.Length);
      for (int i = 0; i < expected.Length; i++) {
        Assert.AreEqual(expected[0], actual[1]);
      }
    }
  }
}
