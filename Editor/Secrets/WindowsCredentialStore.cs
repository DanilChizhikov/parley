using System;
using System.Runtime.InteropServices;
using System.Text;

namespace DTech.Parley.Editor.Secrets
{
	internal sealed class WindowsCredentialStore : ISecretStore
	{
		private const int CredTypeGeneric = 1;
		private const int CredPersistLocalMachine = 2;

		public string Description => "Windows Credential Manager";

		public bool TryGet(string key, out string secret)
		{
			secret = null;
			if (!CredRead(Target(key), CredTypeGeneric, 0, out IntPtr pointer))
			{
				return false;
			}

			try
			{
				NativeCredential credential = Marshal.PtrToStructure<NativeCredential>(pointer);
				if (credential.CredentialBlobSize <= 0 || credential.CredentialBlob == IntPtr.Zero)
				{
					return false;
				}

				byte[] bytes = new byte[credential.CredentialBlobSize];
				Marshal.Copy(credential.CredentialBlob, bytes, 0, bytes.Length);
				secret = Encoding.UTF8.GetString(bytes);
				return secret.Length > 0;
			}
			finally
			{
				CredFree(pointer);
			}
		}

		public bool Set(string key, string secret, out string error)
		{
			byte[] bytes = Encoding.UTF8.GetBytes(secret ?? string.Empty);
			IntPtr blob = Marshal.AllocHGlobal(Math.Max(1, bytes.Length));
			try
			{
				Marshal.Copy(bytes, 0, blob, bytes.Length);
				NativeCredential credential = new NativeCredential
				{
					Type = CredTypeGeneric,
					TargetName = Target(key),
					CredentialBlobSize = bytes.Length,
					CredentialBlob = blob,
					Persist = CredPersistLocalMachine,
					UserName = Environment.UserName,
				};

				if (CredWrite(ref credential, 0))
				{
					error = null;
					return true;
				}

				error = "CredWrite failed with Win32 error " + Marshal.GetLastWin32Error();
				return false;
			}
			finally
			{
				Marshal.FreeHGlobal(blob);
			}
		}

		public bool Delete(string key)
		{
			return CredDelete(Target(key), CredTypeGeneric, 0);
		}

		private static string Target(string key)
		{
			return SecretStores.Service + "/" + key;
		}

		[DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
		private static extern bool CredRead(string target, int type, int flags, out IntPtr credential);

		[DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
		private static extern bool CredWrite(ref NativeCredential credential, int flags);

		[DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
		private static extern bool CredDelete(string target, int type, int flags);

		[DllImport("advapi32.dll", SetLastError = true)]
		private static extern void CredFree(IntPtr buffer);

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		private struct NativeCredential
		{
			public int Flags;
			public int Type;
			public string TargetName;
			public string Comment;
			public long LastWritten;
			public int CredentialBlobSize;
			public IntPtr CredentialBlob;
			public int Persist;
			public int AttributeCount;
			public IntPtr Attributes;
			public string TargetAlias;
			public string UserName;
		}
	}
}