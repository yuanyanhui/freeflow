using System;
using System.Runtime.InteropServices;
using System.Text;

namespace FreeFlow.Services;

public class CredentialService
{
    private const string TargetName = "FreeFlow_GroqApiKey";

    public void SaveApiKey(string apiKey)
    {
        var credential = new PCREDENTIAL
        {
            Type = 1, // CRED_TYPE_GENERIC
            TargetName = TargetName,
            CredentialBlobSize = (uint)Encoding.Unicode.GetByteCount(apiKey),
            CredentialBlob = Marshal.StringToHGlobalUni(apiKey),
            Persist = 2 // CRED_PERSIST_LOCAL_MACHINE
        };

        if (!CredWrite(ref credential, 0))
        {
            throw new Exception($"Failed to write credential. Error code: {Marshal.GetLastWin32Error()}");
        }
    }

    public string? GetApiKey()
    {
        if (CredRead(TargetName, 1, 0, out var credentialPtr))
        {
            var credential = Marshal.PtrToStructure<PCREDENTIAL>(credentialPtr);
            var apiKey = Marshal.PtrToStringUni(credential.CredentialBlob, (int)credential.CredentialBlobSize / 2);
            CredFree(credentialPtr);
            return apiKey;
        }
        return null;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PCREDENTIAL
    {
        public uint Flags;
        public uint Type;
        public string TargetName;
        public string Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string TargetAlias;
        public string UserName;
    }

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredWrite(ref PCREDENTIAL credential, uint flags);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credentialPtr);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern void CredFree(IntPtr buffer);
}
