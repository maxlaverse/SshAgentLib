using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using System.Text;
using System.Security;
using System.Runtime.InteropServices;
using System.Collections.ObjectModel;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Math.EC;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Asn1.Sec;

namespace dlech.PageantSharp
{
  /// <summary>
  /// Implements OpenSSH Agent
  /// </summary>
  /// <remarks>
  /// Inheriting classes should implement the platform specific communication
  /// to get a message from a client and then call AnswerMessage method
  /// </remarks>
  public abstract class Agent : IDisposable
  {
    #region Instance Variables

    private ObservableCollection<ISshKey> mKeyList;
    private SecureString mLockedPassphrase;

    #endregion

    #region Events

    public event LockEventHandler Locked;

    #endregion

    #region Enums

    /* Protocol message number - from PROTOCOL.agent in OpenSSH source code */
    /* note: changed SSH_* to SSH1_* on protocol v1 specific items for clarity */
    public enum Message : byte
    {
      /* Requests from client to agent for protocol 1 key operations */
      SSH1_AGENTC_REQUEST_RSA_IDENTITIES = 1,
      SSH1_AGENTC_RSA_CHALLENGE = 3,
      SSH1_AGENTC_ADD_RSA_IDENTITY = 7,
      SSH1_AGENTC_REMOVE_RSA_IDENTITY = 8,
      SSH1_AGENTC_REMOVE_ALL_RSA_IDENTITIES = 9,
      SSH1_AGENTC_ADD_RSA_ID_CONSTRAINED = 24,

      /* Requests from client to agent for protocol 2 key operations */
      SSH2_AGENTC_REQUEST_IDENTITIES = 11,
      SSH2_AGENTC_SIGN_REQUEST = 13,
      SSH2_AGENTC_ADD_IDENTITY = 17,
      SSH2_AGENTC_REMOVE_IDENTITY = 18,
      SSH2_AGENTC_REMOVE_ALL_IDENTITIES = 19,
      SSH2_AGENTC_ADD_ID_CONSTRAINED = 25,

      /* Key-type independent requests from client to agent */
      SSH_AGENTC_ADD_SMARTCARD_KEY = 20,
      SSH_AGENTC_REMOVE_SMARTCARD_KEY = 21,
      SSH_AGENTC_LOCK = 22,
      SSH_AGENTC_UNLOCK = 23,
      SSH_AGENTC_ADD_SMARTCARD_KEY_CONSTRAINED = 26,

      /* Generic replies from agent to client */
      SSH_AGENT_FAILURE = 5,
      SSH_AGENT_SUCCESS = 6,

      /* Replies from agent to client for protocol 1 key operations */
      SSH1_AGENT_RSA_IDENTITIES_ANSWER = 2,
      SSH1_AGENT_RSA_RESPONSE = 4,

      /* Replies from agent to client for protocol 2 key operations */
      SSH2_AGENT_IDENTITIES_ANSWER = 12,
      SSH2_AGENT_SIGN_RESPONSE = 14
    }

    public enum KeyConstraintType : byte
    {
      /* Key constraint identifiers */
      SSH_AGENT_CONSTRAIN_LIFETIME = 1,
      SSH_AGENT_CONSTRAIN_CONFIRM = 2
    }

    #endregion

    #region Data Types

    public struct KeyConstraint
    {
      public KeyConstraintType Type { get; set; }
      public Object Data { get; set; }
    }

    public struct BlobHeader
    {
      public UInt32 BlobLength { get; set; }
      public Agent.Message Message { get; set; }
    }

    public class LockEventArgs : EventArgs
    {
      public LockEventArgs(bool aIsLocked)
      {
        IsLocked = aIsLocked;
      }
      public bool IsLocked { get; private set; }
    }

    public delegate void LockEventHandler(object aSender, LockEventArgs aEventArgs);

    #endregion

    #region Properties

    /// <summary>
    /// true if agent is locked
    /// </summary>
    public bool IsLocked { get; private set; }

    public ObservableCollection<ISshKey> KeyList
    {
      get
      {
        return mKeyList;
      }
    }

    #endregion

    #region Constructors

    public Agent()
    {
      mKeyList = new ObservableCollection<ISshKey>();
    }

    #endregion

    #region Public Methods

    public bool Lock(byte[] aPassphrase)
    {
      if (IsLocked) {
        // can't lock if already locked
        return false;
      }
      mLockedPassphrase = new SecureString();
      foreach (byte b in aPassphrase) {
        mLockedPassphrase.AppendChar((char)b);
      }
      IsLocked = true;
      OnLocked();
      return true;
    }

    public bool Unlock(byte[] aPassphrase)
    {
      if (!IsLocked) {
        // can't unlock if not locked
        return false;
      }
      if (mLockedPassphrase.Length != aPassphrase.Length) {
        // passwords definitely do not match
        return false;
      }
      IntPtr lockedPassPtr =
          Marshal.SecureStringToGlobalAllocUnicode(mLockedPassphrase);
      for (int i = 0; i < mLockedPassphrase.Length; i++) {
        Int16 lockedPassChar = Marshal.ReadInt16(lockedPassPtr, i * 2);
        if (lockedPassChar != aPassphrase[i]) {
          Marshal.ZeroFreeGlobalAllocUnicode(lockedPassPtr);
          return false;
        }
      }
      Marshal.ZeroFreeGlobalAllocUnicode(lockedPassPtr);
      mLockedPassphrase.Clear();
      IsLocked = false;
      OnLocked();
      return true;
    }

    /// <summary>
    /// Answers the message.
    /// </summary>
    /// <param name='aMessageStream'>
    /// Message stream.
    /// </param>
    /// <remarks>code based on winpgnt.c from PuTTY source code</remarks>
    public void AnswerMessage(Stream aMessageStream)
    {
      BlobParser messageParser = new BlobParser(aMessageStream);
      BlobBuilder responseBuilder = new BlobBuilder();
      BlobHeader header = messageParser.ReadHeader();

      switch (header.Message) {
        case Message.SSH1_AGENTC_REQUEST_RSA_IDENTITIES:
          /*
           * Reply with SSH1_AGENT_RSA_IDENTITIES_ANSWER.
           */

          // TODO implement SSH1_AGENT_RSA_IDENTITIES_ANSWER

          goto default; // failed

        case Message.SSH2_AGENTC_REQUEST_IDENTITIES:
          /*
           * Reply with SSH2_AGENT_IDENTITIES_ANSWER.
           */
          try {
            int keyCount = 0;
            // when locked, we respond with SSH2_AGENT_IDENTITIES_ANSWER, but with no keys
            if (!IsLocked) {
              IEnumerable<ISshKey> v2Keys = from key in KeyList
                                            where key.Version == SshVersion.SSH2
                                            select key;
              foreach (SshKey key in v2Keys) {
                keyCount++;
                responseBuilder.AddBlob(key.CipherKeyPair.Public.ToBlob());
                responseBuilder.AddString(key.Comment);
              }
            }
            responseBuilder.InsertHeader(Message.SSH2_AGENT_IDENTITIES_ANSWER,
              keyCount);
            // TODO may want to check that there is enough room in the message stream
            break; // succeeded
          } catch (Exception ex) {
            Debug.Fail(ex.ToString());
          }
          responseBuilder.Clear();
          goto default; // failed

        case Message.SSH1_AGENTC_RSA_CHALLENGE:
          /*
           * Reply with either SSH1_AGENT_RSA_RESPONSE or
           * SSH_AGENT_FAILURE, depending on whether we have that key
           * or not.
           */

          // TODO implement SSH1_AGENTC_RSA_CHALLENGE

          goto default; // failed

        case Message.SSH2_AGENTC_SIGN_REQUEST:
          /*
           * Reply with either SSH2_AGENT_SIGN_RESPONSE or SSH_AGENT_FAILURE,
           * depending on whether we have that key or not.
           */
          try {
            PinnedByteArray keyBlob = messageParser.ReadBlob();
            PinnedByteArray reqData = messageParser.ReadBlob();

            ISshKey matchingKey =
              KeyList.Where(key => key.Version == SshVersion.SSH2 &&
              key.CipherKeyPair.Public.ToBlob().SequenceEqual(keyBlob.Data)).Single();

            /* create signature */
            ISshKey signKey = matchingKey;
            ISigner signer = signKey.CipherKeyPair.GetSigner();
            string algName = signKey.Algorithm.GetIdentifierString();
            signer.Init(true, signKey.CipherKeyPair.Private);
            signer.BlockUpdate(reqData.Data, 0, reqData.Data.Length);
            byte[] signature = signer.GenerateSignature();

            BlobBuilder signatureBuilder = new BlobBuilder();
            signatureBuilder.AddString(algName);
            signatureBuilder.AddBlob(signature);
            responseBuilder.AddBlob(signatureBuilder.GetBlob());
            responseBuilder.InsertHeader(Message.SSH2_AGENT_SIGN_RESPONSE);
            break; // succeeded
          } catch (InvalidOperationException) {
            // this is expected if there is not a matching key
          } catch (Exception ex) {
            Debug.Fail(ex.ToString());
          }
          responseBuilder.Clear();
          goto default; // failure

        case Message.SSH1_AGENTC_ADD_RSA_IDENTITY:
          /*
           * Add to the list and return SSH_AGENT_SUCCESS, or
           * SSH_AGENT_FAILURE if the key was malformed.
           */

          // TODO implement SSH1_AGENTC_ADD_RSA_IDENTITY

          goto default; // failed

        case Message.SSH2_AGENTC_ADD_IDENTITY:
        case Message.SSH2_AGENTC_ADD_ID_CONSTRAINED:
          /*
           * Add to the list and return SSH_AGENT_SUCCESS, or
           * SSH_AGENT_FAILURE if the key was malformed.
           */

          if (IsLocked) {
            goto default;
          }

          bool constrained = (header.Message ==
              Message.SSH2_AGENTC_ADD_ID_CONSTRAINED);

          try {
            SshKey key = new SshKey();
            key.Version = SshVersion.SSH2;
            key.CipherKeyPair = CreateCipherKeyPair(aMessageStream);
            key.Comment = messageParser.ReadString();

            if (constrained) {
              while (aMessageStream.Position < header.BlobLength) {
                KeyConstraint constraint = new KeyConstraint();
                constraint.Type =
                  (KeyConstraintType)messageParser.ReadByte();
                if (constraint.Type ==
                  KeyConstraintType.SSH_AGENT_CONSTRAIN_LIFETIME) {
                  constraint.Data = messageParser.ReadInt();
                }
                key.Constraints.Add(constraint);
              }
            }
            KeyList.Remove(key.Version, key.CipherKeyPair.Public.ToBlob());
            KeyList.Add(key);
            responseBuilder.InsertHeader(Message.SSH_AGENT_SUCCESS);
            break; // success!            
          } catch (Exception ex) {
            Debug.Fail(ex.ToString());
          }
          goto default; // failed

        case Message.SSH1_AGENTC_REMOVE_RSA_IDENTITY:
        case Message.SSH2_AGENTC_REMOVE_IDENTITY:
          /*
           * Remove from the list and return SSH_AGENT_SUCCESS, or
           * perhaps SSH_AGENT_FAILURE if it wasn't in the list to
           * start with.
           */

          if (IsLocked) {
            goto default;
          }

          SshVersion removeVersion;
          if (header.Message == Message.SSH1_AGENTC_REMOVE_RSA_IDENTITY) {
            removeVersion = SshVersion.SSH1;
          } else if (header.Message == Message.SSH2_AGENTC_REMOVE_IDENTITY) {
            removeVersion = SshVersion.SSH2;
          } else {
            Debug.Fail("Should not get here.");
            goto default;
          }

          try {
            PinnedByteArray rKeyBlob = messageParser.ReadBlob();

            if (KeyList.Remove(removeVersion, rKeyBlob.Data)) {
              responseBuilder.InsertHeader(Message.SSH_AGENT_SUCCESS);
              break; //success!
            }
          } catch (Exception ex) {
            Debug.Fail(ex.ToString());
          }
          goto default; // failed

        case Message.SSH1_AGENTC_REMOVE_ALL_RSA_IDENTITIES:
        case Message.SSH2_AGENTC_REMOVE_ALL_IDENTITIES:
          /*
           * Remove all SSH-1 or SSH-2 keys.
           */

          if (IsLocked) {
            goto default;
          }

          SshVersion removeAllVersion;
          if (header.Message == Message.SSH1_AGENTC_REMOVE_ALL_RSA_IDENTITIES) {
            removeAllVersion = SshVersion.SSH1;
          } else if (header.Message == Message.SSH2_AGENTC_REMOVE_ALL_IDENTITIES) {
            removeAllVersion = SshVersion.SSH2;
          } else {
            Debug.Fail("Should not get here.");
            goto default;
          }

          try {
            List<ISshKey> removeKeyList =
              KeyList.Where(key => key.Version == removeAllVersion).ToList();

            foreach (ISshKey key in removeKeyList) {
              KeyList.Remove(key);
            }
            responseBuilder.InsertHeader(Message.SSH_AGENT_SUCCESS);
            break; //success!
          } catch (Exception ex) {
            Debug.Fail(ex.ToString());
          }
          goto default; // failed

        case Message.SSH_AGENTC_LOCK:
          try {
            PinnedByteArray passphrase = messageParser.ReadBlob();
            bool lockSucceeded = Lock(passphrase.Data);
            passphrase.Clear();
            if (lockSucceeded) {
              responseBuilder.InsertHeader(Message.SSH_AGENT_SUCCESS);
              break;
            }
          } catch (Exception ex) {
            Debug.Fail(ex.ToString());
          }
          goto default;

        case Message.SSH_AGENTC_UNLOCK:
          try {
            PinnedByteArray passphrase = messageParser.ReadBlob();
            bool unlockSucceeded = Unlock(passphrase.Data);
            passphrase.Clear();
            if (unlockSucceeded) {
              responseBuilder.InsertHeader(Message.SSH_AGENT_SUCCESS);
              break;
            }
          } catch (Exception ex) {
            Debug.Fail(ex.ToString());
          }
          goto default;

        default:
          responseBuilder.InsertHeader(Message.SSH_AGENT_FAILURE);
          break;
      }
      /* write response to stream */
      aMessageStream.Position = 0;
      aMessageStream.Write(responseBuilder.GetBlob(), 0, responseBuilder.Length);
    }

    public abstract void Dispose();

    #endregion

    #region Private Methods

    private void OnLocked()
    {
      if (Locked != null) {
        LockEventArgs args = new LockEventArgs(IsLocked);
        Locked(this, args);
      }
    }

    public static AsymmetricCipherKeyPair CreateCipherKeyPair(Stream aSteam)
    {
      BlobParser parser = new BlobParser(aSteam);

      string algorithm = Encoding.UTF8.GetString(parser.ReadBlob().Data);

      switch (algorithm) {
        case PublicKeyAlgorithmExt.ALGORITHM_RSA_KEY:
          BigInteger rsaN = new BigInteger(1, parser.ReadBlob().Data); // modulus
          BigInteger rsaE = new BigInteger(1, parser.ReadBlob().Data); // exponent
          BigInteger rsaD = new BigInteger(1, parser.ReadBlob().Data);
          BigInteger rsaIQMP = new BigInteger(1, parser.ReadBlob().Data);
          BigInteger rsaP = new BigInteger(1, parser.ReadBlob().Data);
          BigInteger rsaQ = new BigInteger(1, parser.ReadBlob().Data);

          /* compute missing parameters */
          BigInteger rsaDP = rsaD.Remainder(rsaP.Subtract(BigInteger.One));
          BigInteger rsaDQ = rsaD.Remainder(rsaQ.Subtract(BigInteger.One));

          RsaKeyParameters rsaPublicKeyParams =
            new RsaKeyParameters(false, rsaN, rsaE);
          RsaPrivateCrtKeyParameters rsaPrivateKeyParams =
            new RsaPrivateCrtKeyParameters(rsaN, rsaE, rsaD, rsaP, rsaQ, rsaDP,
              rsaDQ, rsaIQMP);

          return new AsymmetricCipherKeyPair(rsaPublicKeyParams, rsaPrivateKeyParams);

        case PublicKeyAlgorithmExt.ALGORITHM_DSA_KEY:
          BigInteger dsaP = new BigInteger(1, parser.ReadBlob().Data);
          BigInteger dsaQ = new BigInteger(1, parser.ReadBlob().Data);
          BigInteger dsaG = new BigInteger(1, parser.ReadBlob().Data);
          BigInteger dsaY = new BigInteger(1, parser.ReadBlob().Data); // public key
          BigInteger dsaX = new BigInteger(1, parser.ReadBlob().Data); // private key

          DsaParameters commonParams = new DsaParameters(dsaP, dsaQ, dsaG);
          DsaPublicKeyParameters dsaPublicKeyParams =
            new DsaPublicKeyParameters(dsaY, commonParams);
          DsaPrivateKeyParameters dsaPrivateKeyParams =
            new DsaPrivateKeyParameters(dsaX, commonParams);

          return new AsymmetricCipherKeyPair(dsaPublicKeyParams, dsaPrivateKeyParams);

        case PublicKeyAlgorithmExt.ALGORITHM_ECDSA_SHA2_NISTP256_KEY:
        case PublicKeyAlgorithmExt.ALGORITHM_ECDSA_SHA2_NISTP384_KEY:
        case PublicKeyAlgorithmExt.ALGORITHM_ECDSA_SHA2_NISTP521_KEY:

          string ecdsaCurveName = parser.ReadString();
          byte[] ecdsaPublicKey = parser.ReadBlob().Data;
          BigInteger ecdsaPrivate = new BigInteger(1, parser.ReadBlob().Data);

          switch (ecdsaCurveName) {
            case PublicKeyAlgorithmExt.EC_ALGORITHM_NISTP256:
              ecdsaCurveName = "secp256r1";
              break;
            case PublicKeyAlgorithmExt.EC_ALGORITHM_NISTP384:
              ecdsaCurveName = "secp384r1";
              break;
            case PublicKeyAlgorithmExt.EC_ALGORITHM_NISTP521:
              ecdsaCurveName = "secp521r1";
              break;
            default:
              throw new Exception("Unsupported EC algorithm: " + ecdsaCurveName);
          }
          X9ECParameters ecdsaX9Params = SecNamedCurves.GetByName(ecdsaCurveName);
          ECDomainParameters ecdsaDomainParams =
            new ECDomainParameters(ecdsaX9Params.Curve, ecdsaX9Params.G,
              ecdsaX9Params.N, ecdsaX9Params.H);
          ECPoint ecdsaPoint = ecdsaX9Params.Curve.DecodePoint(ecdsaPublicKey);
          ECPublicKeyParameters ecPublicKeyParams =
            new ECPublicKeyParameters(ecdsaPoint, ecdsaDomainParams);
          ECPrivateKeyParameters ecPrivateKeyParams =
            new ECPrivateKeyParameters(ecdsaPrivate, ecdsaDomainParams);

          return new AsymmetricCipherKeyPair(ecPublicKeyParams, ecPrivateKeyParams);
    
        default:
          // unsupported encryption algorithm
          throw new Exception("Unsupported algorithm");
      }
    }

    #endregion
  }
}

