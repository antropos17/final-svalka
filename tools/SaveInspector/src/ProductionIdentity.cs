using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace JunkToNeon.SaveInspector
{
    internal static class ProductionIdentity
    {
        internal static Obj Read()
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("production-sources.txt"))
            using (var reader = new StreamReader(stream, Encoding.UTF8, true))
            {
                // Normalize only manifest line endings/separators; per-file hashes identify
                // exact source bytes. No repository or disk access occurs at inspection.
                string manifest = reader.ReadToEnd().Replace("\\", "/").Replace("\r\n", "\n");
                int count = manifest.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
                if (count == 0) throw new InvalidOperationException("Missing production source identity");
                return new Obj().Add("algorithm", "sha256-of-utf8-path-equals-source-sha256-lines-lf")
                    .Add("fileCount", count).Add("sha256", InputGuard.HashText(manifest))
                    .Add("scope", "actual-linked-pure-sources-at-build; not-runtime-or-git-qualification");
            }
        }
    }
}
