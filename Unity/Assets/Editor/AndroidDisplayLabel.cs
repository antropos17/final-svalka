using System;
using System.IO;
using System.Text;
using System.Xml;
using UnityEditor.Android;

namespace JunkToNeon.Editor
{
    // Keep Unity's product name and save identity unchanged; only the Android
    // launcher application label is changed after Gradle project generation.
    public sealed class AndroidDisplayLabel : IPostGenerateGradleAndroidProject
    {
        const string AndroidNamespace = "http://schemas.android.com/apk/res/android";
        const string Label = "Junk to Neon";
        const string GameActivity = "com.unity3d.player.UnityPlayerGameActivity";
        const string OriginalLabel = "@string/app_name";

        public int callbackOrder => 0;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var libraryDirectory = new DirectoryInfo(Path.GetFullPath(path));
            if (libraryDirectory.Name != "unityLibrary" || libraryDirectory.Parent == null)
                throw new InvalidOperationException("Unexpected Unity Android Gradle project layout.");

            string launcherPath = Path.Combine(libraryDirectory.Parent.FullName, "launcher", "src", "main", "AndroidManifest.xml");
            string libraryPath = Path.Combine(libraryDirectory.FullName, "src", "main", "AndroidManifest.xml");
            byte[] launcherBytes = File.ReadAllBytes(launcherPath);
            var utf8 = new UTF8Encoding(false, true);
            string launcherText = utf8.GetString(launcherBytes);
            XmlDocument launcher = ReadManifest(launcherBytes);
            XmlDocument library = ReadManifest(File.ReadAllBytes(libraryPath));
            XmlElement launcherApplication = Application(launcher);
            XmlElement libraryApplication = Application(library);

            if (launcher.GetElementsByTagName("activity").Count != 0 ||
                launcher.GetElementsByTagName("activity-alias").Count != 0 ||
                library.GetElementsByTagName("activity").Count != 1 ||
                library.GetElementsByTagName("activity-alias").Count != 0 ||
                libraryApplication.HasAttribute("label", AndroidNamespace))
                throw new InvalidOperationException("Unexpected Android launcher manifest structure.");

            var activity = library.GetElementsByTagName("activity")[0] as XmlElement;
            if (activity == null || activity.ParentNode != libraryApplication ||
                activity.GetAttribute("name", AndroidNamespace) != GameActivity ||
                activity.HasAttribute("label", AndroidNamespace))
                throw new InvalidOperationException("Unexpected Android launcher activity or label.");

            var filters = activity.GetElementsByTagName("intent-filter");
            if (filters.Count != 1 || filters[0].ParentNode != activity)
                throw new InvalidOperationException("Unexpected Android launcher intent filters.");
            var filter = filters[0] as XmlElement;
            if (filter == null || filter.HasAttribute("label", AndroidNamespace) ||
                !HasSingleIntentName(filter, "action", "android.intent.action.MAIN") ||
                !HasSingleIntentName(filter, "category", "android.intent.category.LAUNCHER"))
                throw new InvalidOperationException("Unexpected Android launcher intent or label.");

            string current = launcherApplication.GetAttribute("label", AndroidNamespace);
            if (current == Label) return;
            if (current != OriginalLabel)
                throw new InvalidOperationException("Unexpected Android application label.");

            // Replace the one existing attribute without serializing or changing
            // the manifest's other bytes, namespace prefixes, or line endings.
            const string oldAttribute = "android:label=\"@string/app_name\"";
            const string newAttribute = "android:label=\"Junk to Neon\"";
            int index = launcherText.IndexOf(oldAttribute, StringComparison.Ordinal);
            if (index < 0 || launcherText.IndexOf(oldAttribute, index + oldAttribute.Length, StringComparison.Ordinal) >= 0)
                throw new InvalidOperationException("Unexpected Android application label encoding.");
            string updated = launcherText.Substring(0, index) + newAttribute + launcherText.Substring(index + oldAttribute.Length);
            byte[] updatedBytes = utf8.GetBytes(updated);
            if (Application(ReadManifest(updatedBytes)).GetAttribute("label", AndroidNamespace) != Label)
                throw new InvalidOperationException("Android application label replacement did not match the application.");
            File.WriteAllBytes(launcherPath, updatedBytes);
        }

        static XmlDocument ReadManifest(byte[] bytes)
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            var document = new XmlDocument { XmlResolver = null };
            using (var stream = new MemoryStream(bytes, false))
            using (var reader = XmlReader.Create(stream, settings)) document.Load(reader);
            if (document.DocumentElement == null || document.DocumentElement.Name != "manifest")
                throw new InvalidOperationException("Unexpected Android manifest root.");
            return document;
        }

        static XmlElement Application(XmlDocument manifest)
        {
            var applications = manifest.GetElementsByTagName("application");
            if (applications.Count != 1 || applications[0].ParentNode != manifest.DocumentElement)
                throw new InvalidOperationException("Unexpected Android application element.");
            return (XmlElement)applications[0];
        }

        static bool HasSingleIntentName(XmlElement filter, string tag, string expected)
        {
            var elements = filter.GetElementsByTagName(tag);
            return elements.Count == 1 && elements[0].ParentNode == filter &&
                ((XmlElement)elements[0]).GetAttribute("name", AndroidNamespace) == expected;
        }
    }
}
