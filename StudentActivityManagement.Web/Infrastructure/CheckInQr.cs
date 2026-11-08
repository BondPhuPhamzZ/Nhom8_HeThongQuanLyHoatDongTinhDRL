using QRCoder;

namespace StudentActivityManagement.Web.Infrastructure;

/// <summary>
/// US-35: the QR code shown for a session carries <c>nhom8-checkin:v1:{activityId}:{sessionId}:{code}</c>, the same
/// format the Mobile app parses. It only saves typing: the API still checks the code, the window and the registration.
/// </summary>
public static class CheckInQr
{
    public const string Prefix = "nhom8-checkin:v1";

    public static string Payload(int activityId, int sessionId, string code) => $"{Prefix}:{activityId}:{sessionId}:{code}";

    public static byte[] Png(string payload, int pixelsPerModule) =>
        PngByteQRCodeHelper.GetQRCode(payload, QRCodeGenerator.ECCLevel.M, pixelsPerModule);
}
