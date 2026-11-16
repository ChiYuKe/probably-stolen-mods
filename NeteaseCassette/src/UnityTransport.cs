using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;

namespace NeteaseCassette
{
    // NeteaseClient 的表单请求 -> UnityWebRequest。
    // 请求头客户端自己填好，这里只管发。
    public class UnityTransport : INcTransport
    {
        public const string UserAgent =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Safari/537.36";

        public IEnumerator Send(NcRequest req, Action<NcResponse> onDone)
        {
            var resp = new NcResponse();
            UnityWebRequest uwr = null;
            UnityWebRequestAsyncOperation op = null;

            try
            {
                string body = FormEncode(req.Form);
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(body ?? string.Empty);
                uwr = new UnityWebRequest(req.Url, "POST");
                uwr.uploadHandler = new UploadHandlerRaw(bytes);
                uwr.downloadHandler = new DownloadHandlerBuffer();
                uwr.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded; charset=UTF-8");
                uwr.SetRequestHeader("Accept", "*/*");
                uwr.SetRequestHeader("Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8");
                uwr.timeout = 20;
                if (req.Headers != null)
                {
                    foreach (var kv in req.Headers)
                    {
                        if (!string.IsNullOrEmpty(kv.Key) && kv.Value != null)
                        {
                            uwr.SetRequestHeader(kv.Key, kv.Value);
                        }
                    }
                }
                op = uwr.SendWebRequest();
            }
            catch (Exception e)
            {
                resp.Error = e.Message;
                if (uwr != null) uwr.Dispose();
                onDone?.Invoke(resp);
                yield break;
            }

            while (op != null && !op.isDone)
            {
                yield return null;
            }

            try
            {
                if (uwr != null)
                {
                    resp.Status = uwr.responseCode;
                    if (uwr.downloadHandler != null)
                    {
                        resp.Text = uwr.downloadHandler.text;
                    }
                    resp.SetCookie = ReadSetCookie(uwr);
                    if (uwr.result != UnityWebRequest.Result.Success && string.IsNullOrEmpty(resp.Text))
                    {
                        resp.Error = string.IsNullOrEmpty(uwr.error) ? ("HTTP " + uwr.responseCode) : uwr.error;
                    }
                }
            }
            catch (Exception e)
            {
                resp.Error = e.Message;
            }
            finally
            {
                if (uwr != null) uwr.Dispose();
            }

            onDone?.Invoke(resp);
        }

        private static string[] ReadSetCookie(UnityWebRequest uwr)
        {
            if (uwr == null) return null;
            var headers = uwr.GetResponseHeaders();
            if (headers == null) return null;
            var values = new List<string>();
            foreach (var kv in headers)
            {
                // 响应头的键大小写看服务器，同名头还可能被合并，干脆全收
                if (string.Equals(kv.Key, "Set-Cookie", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(kv.Value))
                {
                    values.Add(kv.Value);
                }
            }
            return values.Count == 0 ? null : values.ToArray();
        }

        public static string FormEncode(Dictionary<string, string> form)
        {
            if (form == null || form.Count == 0) return string.Empty;
            var sb = new System.Text.StringBuilder();
            foreach (var kv in form)
            {
                if (sb.Length > 0) sb.Append('&');
                sb.Append(Uri.EscapeDataString(kv.Key ?? string.Empty));
                sb.Append('=');
                sb.Append(Uri.EscapeDataString(kv.Value ?? string.Empty));
            }
            return sb.ToString();
        }
    }
}
