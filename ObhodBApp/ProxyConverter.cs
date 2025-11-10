using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Web;

namespace ObhodBApp
{
    public static class ClashProxyConverter
    {
        public static string ConvertToClashProxyBlock(string input)
        {
            if (input.StartsWith("vless://", StringComparison.OrdinalIgnoreCase))
                return ConvertVless(input);
            if (input.StartsWith("vmess://", StringComparison.OrdinalIgnoreCase))
                return ConvertVmess(input);
            if (input.StartsWith("trojan://", StringComparison.OrdinalIgnoreCase))
                return ConvertTrojan(input);
            if (input.StartsWith("ss://", StringComparison.OrdinalIgnoreCase))
                return ConvertShadowsocks(input);
            if (input.StartsWith("socks5://", StringComparison.OrdinalIgnoreCase))
                return ConvertSocks5(input);

            throw new Exception("Неверный формат");
        }

        public static string ConvertMultipleToClashYaml(string uri)
        {
            var sb = new StringBuilder();
            sb.AppendLine("proxies:");
            sb.Append(ConvertToClashProxyBlock(uri));
            sb.Append("\nproxy-groups:\n  - name: PROXY\n    type: select\n    proxies:\n      - main");
            return sb.ToString();
        }
        
        public static string GetProfileName(string uri)
        {
            try
            {
                if (uri.StartsWith("vmess://", StringComparison.OrdinalIgnoreCase))
                {
                    string encoded = uri["vmess://".Length..];
                    string json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("ps", out var ps))
                        return ps.GetString() ?? "main";
                }
                else
                {
                    var u = new Uri(uri);
                    if (u.Fragment.Length > 1)
                        return Uri.UnescapeDataString(u.Fragment[1..]);
                }
            }
            catch { }
            return "main";
        }

        private static string ConvertVless(string uriStr)
        {
            var uri = new Uri(uriStr);
            var q = HttpUtility.ParseQueryString(uri.Query);

            string uuid = uri.UserInfo;
            string server = uri.Host;
            int port = uri.Port;

            var sb = new StringBuilder();
            sb.AppendLine($"  - name: \"main\"");
            sb.AppendLine($"    type: vless");
            sb.AppendLine($"    server: {server}");
            sb.AppendLine($"    port: {port}");
            sb.AppendLine($"    uuid: {uuid}");
            sb.AppendLine($"    network: {q["type"] ?? "tcp"}");

            if (q["security"] == "reality")
            {
                sb.AppendLine($"    tls: true");
                sb.AppendLine($"    reality-opts:");
                if (!string.IsNullOrEmpty(q["pbk"]))
                    sb.AppendLine($"      public-key: {q["pbk"]}");
                if (!string.IsNullOrEmpty(q["sid"]))
                    sb.AppendLine($"      short-id: {q["sid"]}");
            }
            if (!string.IsNullOrEmpty(q["sni"]))
                sb.AppendLine($"    servername: {q["sni"]}");
            if (!string.IsNullOrEmpty(q["fp"]))
                sb.AppendLine($"    client-fingerprint: {q["fp"]}");
            if (!string.IsNullOrEmpty(q["flow"]))
                sb.AppendLine($"    flow: {q["flow"]}");
            sb.AppendLine($"    udp: true");
            return sb.ToString();
        }

        private static string ConvertVmess(string uriStr)
        {
            string encoded = uriStr["vmess://".Length..];
            string json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string add = root.GetProperty("add").GetString()!;
            string port = root.GetProperty("port").GetString()!;
            string id = root.GetProperty("id").GetString()!;
            string net = root.GetProperty("net").GetString()!;
            string tls = root.TryGetProperty("tls", out var tlsVal) ? tlsVal.GetString()! : "";

            var sb = new StringBuilder();
            sb.AppendLine($"  - name: \"main\"");
            sb.AppendLine($"    type: vmess");
            sb.AppendLine($"    server: {add}");
            sb.AppendLine($"    port: {port}");
            sb.AppendLine($"    uuid: {id}");
            sb.AppendLine($"    alterId: 0");
            sb.AppendLine($"    cipher: auto");
            sb.AppendLine($"    network: {net}");
            if (!string.IsNullOrEmpty(tls) && tls.Equals("tls", StringComparison.OrdinalIgnoreCase))
                sb.AppendLine($"    tls: true");
            sb.AppendLine($"    udp: true");
            return sb.ToString();
        }

        private static string ConvertTrojan(string uriStr)
        {
            var uri = new Uri(uriStr);
            string password = uri.UserInfo;
            string server = uri.Host;
            int port = uri.Port;
            var q = HttpUtility.ParseQueryString(uri.Query);

            var sb = new StringBuilder();
            sb.AppendLine($"  - name: \"main\"");
            sb.AppendLine($"    type: trojan");
            sb.AppendLine($"    server: {server}");
            sb.AppendLine($"    port: {port}");
            sb.AppendLine($"    password: {password}");
            if (!string.IsNullOrEmpty(q["sni"]))
                sb.AppendLine($"    sni: {q["sni"]}");
            sb.AppendLine($"    udp: true");
            return sb.ToString();
        }

        private static string ConvertShadowsocks(string uriStr)
        {
            string raw = uriStr["ss://".Length..];

            if (raw.Contains("#"))
                raw = raw.Split('#')[0]; // игнорируем имя — у нас всегда main

            string decoded = raw.Contains("@")
                ? raw
                : Encoding.UTF8.GetString(Convert.FromBase64String(raw));

            string[] parts2 = decoded.Split('@');
            string[] auth = parts2[0].Split(':');
            string[] addr = parts2[1].Split(':');
            string method = auth[0];
            string password = auth[1];
            string server = addr[0];
            string port = addr[1];

            var sb = new StringBuilder();
            sb.AppendLine($"  - name: \"main\"");
            sb.AppendLine($"    type: ss");
            sb.AppendLine($"    server: {server}");
            sb.AppendLine($"    port: {port}");
            sb.AppendLine($"    cipher: {method}");
            sb.AppendLine($"    password: {password}");
            sb.AppendLine($"    udp: true");
            return sb.ToString();
        }

        private static string ConvertSocks5(string uriStr)
        {
            var uri = new Uri(uriStr);
            string server = uri.Host;
            int port = uri.Port;
            string user = null, pass = null;

            if (!string.IsNullOrEmpty(uri.UserInfo) && uri.UserInfo.Contains(":"))
            {
                var creds = uri.UserInfo.Split(':');
                user = creds[0];
                pass = creds.Length > 1 ? creds[1] : null;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"  - name: \"main\"");
            sb.AppendLine($"    type: socks5");
            sb.AppendLine($"    server: {server}");
            sb.AppendLine($"    port: {port}");
            if (!string.IsNullOrEmpty(user))
                sb.AppendLine($"    username: {user}");
            if (!string.IsNullOrEmpty(pass))
                sb.AppendLine($"    password: {pass}");
            sb.AppendLine($"    udp: true");
            return sb.ToString();
        }
    }
}
