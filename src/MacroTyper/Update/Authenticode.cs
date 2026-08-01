using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace MacroTyper.Update;

/// <summary>서명과 파일 내용이 맞는지에 대한 답.</summary>
internal enum SignatureState
{
    /// <summary>서명이 내용과 맞고 발급자까지 신뢰된다.</summary>
    Valid,

    /// <summary>
    /// 서명이 내용과 맞지만 발급자를 신뢰하지 않는다.
    /// 자체 서명 인증서를 쓰는 우리에게는 이게 정상이다. 지문 대조로 발급자를 대신 확인한다.
    /// </summary>
    UntrustedPublisher,

    /// <summary>서명이 없다.</summary>
    Missing,

    /// <summary>서명은 있는데 내용과 맞지 않는다. 서명 이후 파일이 바뀌었다는 뜻이다.</summary>
    Tampered,

    Unknown,
}

/// <summary>
/// 내려받은 exe 가 정말 우리가 서명한 것인지 확인한다.
///
/// HTTPS 는 "GitHub 에서 왔다"까지만 말해 준다. 그 릴리즈에 무엇이 올라가 있는지는
/// GitHub 계정을 쥔 쪽이 정한다. 서명 개인 키는 저장소에도 GitHub 에도 없으므로,
/// 지금 도는 exe 와 같은 키로 서명되었는지 대조하면 그 한 겹을 더 막을 수 있다.
/// </summary>
internal static class Authenticode
{
    private static readonly Guid GenericVerifyV2 = new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

    private const uint WtdUiNone = 2;
    private const uint WtdRevokeNone = 0;
    private const uint WtdChoiceFile = 1;
    private const uint WtdStateActionVerify = 1;
    private const uint WtdStateActionClose = 2;
    private const uint WtdSaferFlag = 0x100;
    private const uint WtdCacheOnlyUrlRetrieval = 0x1000;

    private const int TrustENoSignature = unchecked((int)0x800B0100);
    private const int TrustEBadDigest = unchecked((int)0x80096010);
    private const int TrustESubjectFormUnknown = unchecked((int)0x800B0003);
    private const int TrustESubjectNotTrusted = unchecked((int)0x800B0004);
    private const int CertEUntrustedRoot = unchecked((int)0x800B0109);
    private const int CertEChaining = unchecked((int)0x800B010A);
    private const int CertEExpired = unchecked((int)0x800B0101);

    private static readonly nint NoWindow = -1;

    /// <summary>
    /// 서명 검사. 발급자 신뢰 여부와 무관하게 "내용이 서명과 맞는가"를 알려 준다.
    /// </summary>
    public static SignatureState Check(string path)
    {
        var fileInfo = new WintrustFileInfo
        {
            pcwszFilePath = path,
            hFile = nint.Zero,
            pgKnownSubject = nint.Zero,
        };
        fileInfo.cbStruct = (uint)Marshal.SizeOf<WintrustFileInfo>();

        nint fileInfoPtr = Marshal.AllocHGlobal((int)fileInfo.cbStruct);

        try
        {
            Marshal.StructureToPtr(fileInfo, fileInfoPtr, fDeleteOld: false);

            var data = new WintrustData
            {
                dwUIChoice = WtdUiNone,
                fdwRevocationChecks = WtdRevokeNone,
                dwUnionChoice = WtdChoiceFile,
                pFile = fileInfoPtr,
                dwStateAction = WtdStateActionVerify,
                // 인증서 폐기 목록을 받으러 네트워크에 나가지 않는다.
                // 오프라인에서 몇십 초를 멈춰 있는 것을 막는다.
                dwProvFlags = WtdSaferFlag | WtdCacheOnlyUrlRetrieval,
            };
            data.cbStruct = (uint)Marshal.SizeOf<WintrustData>();

            int result;
            Guid action = GenericVerifyV2;

            try
            {
                result = WinVerifyTrust(NoWindow, ref action, ref data);
            }
            finally
            {
                // 검사에 실패했더라도 반드시 닫는다. 안 닫으면 핸들이 샌다.
                data.dwStateAction = WtdStateActionClose;
                WinVerifyTrust(NoWindow, ref action, ref data);
            }

            return result switch
            {
                0 => SignatureState.Valid,
                CertEUntrustedRoot or TrustESubjectNotTrusted or CertEChaining or CertEExpired
                    => SignatureState.UntrustedPublisher,
                TrustENoSignature or TrustESubjectFormUnknown => SignatureState.Missing,
                TrustEBadDigest => SignatureState.Tampered,
                _ => SignatureState.Unknown,
            };
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
        {
            return SignatureState.Unknown;
        }
        finally
        {
            Marshal.FreeHGlobal(fileInfoPtr);
        }
    }

    /// <summary>
    /// 서명한 인증서의 SHA256 지문. 서명이 없으면 <c>null</c>이다.
    ///
    /// 이 값만으로는 내용이 바뀌지 않았다고 말할 수 없다. 서명 블록은 그대로 두고
    /// 내용만 갈아치울 수 있기 때문이다. 반드시 <see cref="Check"/>와 같이 쓴다.
    /// </summary>
    public static string? Thumbprint(string path)
    {
        try
        {
            using X509Certificate certificate = X509Certificate.CreateFromSignedFile(path);
            return certificate.GetCertHashString(HashAlgorithmName.SHA256);
        }
        catch (Exception e) when (e is CryptographicException or PlatformNotSupportedException or IOException)
        {
            return null;
        }
    }

    [DllImport("wintrust.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int WinVerifyTrust(nint hwnd, ref Guid actionId, ref WintrustData data);

    [StructLayout(LayoutKind.Sequential)]
    private struct WintrustFileInfo
    {
        public uint cbStruct;
        [MarshalAs(UnmanagedType.LPWStr)] public string pcwszFilePath;
        public nint hFile;
        public nint pgKnownSubject;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WintrustData
    {
        public uint cbStruct;
        public nint pPolicyCallbackData;
        public nint pSIPClientData;
        public uint dwUIChoice;
        public uint fdwRevocationChecks;
        public uint dwUnionChoice;
        public nint pFile;
        public uint dwStateAction;
        public nint hWVTStateData;
        public nint pwszURLReference;
        public uint dwProvFlags;
        public uint dwUIContext;
        public nint pSignatureSettings;
    }
}
