using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Lidgren.Network
{
	/// <summary>
	/// DES encryption
	/// </summary>
	public class NetDESEncryption : INetEncryption
	{
		private readonly byte[] m_key;
		private readonly byte[] m_iv;
		private readonly int m_bitSize;
		private static readonly List<int> s_keysizes;
		private static readonly List<int> s_blocksizes;

		static NetDESEncryption()
		{

			DESCryptoServiceProvider des = new DESCryptoServiceProvider();
			List<int> temp = new List<int>();
			foreach (KeySizes keysize in des.LegalKeySizes)
			{
				for (int i = keysize.MinSize; i <= keysize.MaxSize; i += keysize.SkipSize)
				{
					if (!temp.Contains(i))
						temp.Add(i);
					if (i == keysize.MaxSize)
						break;
				}
			}
			s_keysizes = temp;
			temp = new List<int>();
			foreach (KeySizes keysize in des.LegalBlockSizes)
			{
				for (int i = keysize.MinSize; i <= keysize.MaxSize; i += keysize.SkipSize)
				{

					if (!temp.Contains(i))
						temp.Add(i);
					if (i == keysize.MaxSize)
						break;
				}
			}
			s_blocksizes = temp;
		}

		/// <summary>
		/// NetDESEncryption constructor
		/// </summary>
		public NetDESEncryption(byte[] key, byte[] iv)
		{
			if (!s_keysizes.Contains(key.Length * 8))
				throw new NetException(string.Format("Not a valid key size. (Valid values are: {0})", NetUtility.MakeCommaDelimitedList(s_keysizes)));

			if (!s_blocksizes.Contains(iv.Length * 8))
				throw new NetException(string.Format("Not a valid iv size. (Valid values are: {0})",  NetUtility.MakeCommaDelimitedList(s_blocksizes)));

			m_key = key;
			m_iv = iv;
			m_bitSize = m_key.Length * 8;
		}

		/// <summary>
		/// NetDESEncryption constructor
		/// </summary>
		public NetDESEncryption(string key, int bitsize)
		{
			if (!s_keysizes.Contains(bitsize))
				throw new NetException(string.Format("Not a valid key size. (Valid values are: {0})", NetUtility.MakeCommaDelimitedList(s_keysizes)));

			byte[] entropy = Encoding.UTF32.GetBytes(key);
			// I know hardcoding salts is bad, but in this case I think it is acceptable.
			HMACSHA512 hmacsha512 = new HMACSHA512(Convert.FromBase64String("i88NEiez3c50bHqr3YGasDc4p8jRrxJAaiRiqixpvp4XNAStP5YNoC2fXnWkURtkha6M8yY901Gj07IRVIRyGL=="));
			hmacsha512.Initialize();
			for (int i = 0; i < 1000; i++)
			{
				entropy = hmacsha512.ComputeHash(entropy);
			}
			int keylen = bitsize / 8;
			m_key = new byte[keylen];
			Buffer.BlockCopy(entropy, 0, m_key, 0, keylen);
			m_iv = new byte[s_blocksizes[0] / 8];

			Buffer.BlockCopy(entropy, entropy.Length - m_iv.Length - 1, m_iv, 0, m_iv.Length);
			m_bitSize = bitsize;
		}

		/// <summary>
		/// NetDESEncryption constructor
		/// </summary>
		public NetDESEncryption(string key)
			: this(key, s_keysizes[0])
		{
		}

		/// <summary>
		/// Encrypt outgoing message
		/// </summary>
		public bool Encrypt(NetOutgoingMessage msg)
		{
			try
			{
				// nested usings are fun!
				using (DESCryptoServiceProvider desCryptoServiceProvider = new DESCryptoServiceProvider { KeySize = m_bitSize, Mode = CipherMode.CBC })
				{
					using (ICryptoTransform cryptoTransform = desCryptoServiceProvider.CreateEncryptor(m_key, m_iv))
					{
						var memoryStream = new MemoryStream();
						using (CryptoStream cryptoStream = new CryptoStream(memoryStream, cryptoTransform, CryptoStreamMode.Write))
						{
							cryptoStream.Write(msg.m_data, 0, msg.m_data.Length);
							cryptoStream.Close();
							msg.m_data = memoryStream.ToArray();
						}
					}
				}

			}
			catch
			{
				return false;
			}
			return true;
		}

		/// <summary>
		/// Decrypt incoming message
		/// </summary>
		public bool Decrypt(NetIncomingMessage msg)
		{
			try
			{
				// nested usings are fun!
				using (DESCryptoServiceProvider desCryptoServiceProvider = new DESCryptoServiceProvider { KeySize = m_bitSize, Mode = CipherMode.CBC })
				{
					using (ICryptoTransform cryptoTransform = desCryptoServiceProvider.CreateDecryptor(m_key, m_iv))
					{
						var memoryStream = new MemoryStream();
						using (CryptoStream cryptoStream = new CryptoStream(memoryStream, cryptoTransform, CryptoStreamMode.Write))
						{
							cryptoStream.Write(msg.m_data, 0, msg.m_data.Length);
							cryptoStream.Close();
							msg.m_data = memoryStream.ToArray();
						}
					}
				}

			}
			catch
			{
				return false;
			}
			return true;
		}
	}
}