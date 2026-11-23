UnityEditor.AssetDatabase.Refresh();
var paths=System.IO.Directory.GetFiles(UnityEngine.Application.dataPath+"/Resources/Art/StyleC","*.png",System.IO.SearchOption.AllDirectories);
foreach(var full in paths){
 var path="Assets"+full.Substring(UnityEngine.Application.dataPath.Length).Replace('\\','/');
 var importer=(UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(path);
 importer.textureType=UnityEditor.TextureImporterType.Sprite;importer.spriteImportMode=UnityEditor.SpriteImportMode.Single;
 importer.alphaIsTransparency=true;importer.mipmapEnabled=true;importer.isReadable=false;importer.npotScale=UnityEditor.TextureImporterNPOTScale.None;
 importer.textureCompression=UnityEditor.TextureImporterCompression.Uncompressed;importer.filterMode=UnityEngine.FilterMode.Trilinear;importer.maxTextureSize=2048;
 importer.spritePixelsPerUnit=100;importer.spriteBorder=path.Contains("/Buttons/")&&!path.Contains("round-")?new UnityEngine.Vector4(60,45,60,45):UnityEngine.Vector4.zero;
 importer.SaveAndReimport();
}
return new {imported=paths.Length,playing=UnityEditor.EditorApplication.isPlaying};
