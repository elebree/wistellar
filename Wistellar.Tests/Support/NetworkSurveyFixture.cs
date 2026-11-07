namespace Wistellar.Tests.Support;

/// <summary>
/// Network Survey CSV logs laid out like the app writes them: a '#' comment block, the column
/// header, then records. The rows come from a phone whose locale uses a decimal comma, so the
/// location fields are quoted while signal values keep their decimal point.
/// </summary>
internal static class NetworkSurveyFixture
{
    public const string Banner = "# Created by Network Survey version=1.0.0";

    public const string WifiColumns = "deviceTime,latitude,longitude,altitude,speed,accuracy,missionId,recordNumber,sourceAddress,destinationAddress,bssid,beaconInterval,serviceSetType,ssid,supportedRates,extendedSupportedRates,cipherSuites,akmSuites,encryptionType,wps,channel,frequencyMhz,signalStrength,snr,nodeType,standard,passpoint,bandwidth,deviceSerialNumber,locationAge";
    public const string BluetoothColumns = "deviceTime,latitude,longitude,altitude,speed,accuracy,missionId,recordNumber,sourceAddress,destinationAddress,signalStrength,txPower,technology,supportedTechnologies,otaDeviceName,channel,deviceSerialNumber,locationAge,addressType,deviceClass,serviceUuids,companyId,mfgData";
    /// <summary>CSV version 0.4.0, from before the plmn column was added.</summary>
    public const string LteColumns = "deviceTime,latitude,longitude,altitude,speed,accuracy,missionId,recordNumber,groupNumber,mcc,mnc,tac,eci,earfcn,pci,rsrp,rsrq,ta,servingCell,lteBandwidth,provider,signalStrength,cqi,slot,snr,deviceSerialNumber,locationAge";
    public const string LtePlmnColumns = LteColumns + ",plmn,connectionStatus,cellBandwidthsKhz";

    public const string CommaLocation = "\"52,520000\",\"13,405000\",\"34,5\",\"0,23\",11";
    public const string DotLocation = "52.520000,13.405000,34.5,0.23,11";
    public const string NoFixLocation = "\"0,000000\",\"0,000000\",0,0,0";
    private const string Mission = "NS 0000000000000000 20260329-195802";
    private const string Serial = "0000000000000000";

    public static string Document(string columns, params string[] rows) =>
        string.Join("\r\n", [Banner, "# CSV Version=0.4.0", columns, .. rows]) + "\r\n";

    public static string WifiRow(string bssid = "02:00:00:00:00:01", string location = CommaLocation,
        string encryption = "WPA3", string wps = "false", string signal = "-70.0") =>
        $"2026-03-29T19:58:26.287411+02:00,{location},{Mission},1,,,{bssid},,,Test network,,,,,{encryption},{wps},1,2412,{signal},,,IEEE80211AX,,MHZ_20,{Serial},11136";

    public static string BluetoothRow(string technology = "UNKNOWN", string supported = "DUAL",
        string companyId = "6", string mfgData = "0600000000000000") =>
        $"2026-03-29T19:58:21.811151+02:00,{CommaLocation},{Mission},1,02:00:00:00:00:02,,-65.0,,{technology},{supported},Test device,,{Serial},6660,RANDOM,,,{companyId},{mfgData}";

    public static string LteRow(string location = CommaLocation, string mcc = "262", string mnc = "1", string eci = "1000000") =>
        $"2026-03-29T19:58:23.304968+02:00,{location},{Mission},1,5,{mcc},{mnc},100,{eci},100,262,-117.0,-10.0,,true,MHZ_20,Test Provider,-51.0,,,3.0,{Serial},8154";

    public static string LtePlmnRow(string mcc, string mnc, string plmn) => LteRow(mcc: mcc, mnc: mnc) + $",{plmn},,";
}
