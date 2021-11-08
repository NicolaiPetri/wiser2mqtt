using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace wiser2mqtt
{
    public class CurlWrapper
    {
        public static string DoCurlRequest(string url, string method = "GET", string username=null, string password=null, string[] headers=null)
        {
            // Start the child process.
            Process p = new Process();
            // Redirect the output stream of the child process.
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.FileName = $"/usr/bin/curl";
            string hdrs = "";
            foreach (var h in headers)
            {
                hdrs += $"-H \"{h}\""; 
            }
            p.StartInfo.Arguments = $"--insecure {hdrs} {url}";
            p.Start();
            // Do not wait for the child process to exit before
            // reading to the end of its redirected stream.
            // p.WaitForExit();
            // Read the output stream first and then wait.
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            return output;
        }
    }
}
