$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies @([System.Drawing.Bitmap].Assembly.Location,[System.Drawing.Rectangle].Assembly.Location) @'
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
public static class SectionAlpha {
 public static int[] Bounds(Bitmap image) {
  var data=image.LockBits(new Rectangle(0,0,image.Width,image.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
  var bytes=new byte[data.Stride*image.Height];Marshal.Copy(data.Scan0,bytes,0,bytes.Length);image.UnlockBits(data);
  int left=image.Width,top=image.Height,right=-1,bottom=-1,border=0,count=0;
  for(int y=0;y<image.Height;y++)for(int x=0;x<image.Width;x++) {
   int alpha=bytes[y*data.Stride+x*4+3];
   if((x==0||y==0||x==image.Width-1||y==image.Height-1)&&alpha!=0)border++;
   if(alpha<=8)continue;count++;left=System.Math.Min(left,x);right=System.Math.Max(right,x);top=System.Math.Min(top,y);bottom=System.Math.Max(bottom,y);
  }
  return new[]{left,top,right,bottom,border,count};
 }
}
'@
$pack = Join-Path $PWD 'art_source/jtn/2026-11-15-style-c-sections-v006'
$target = Join-Path $PWD 'Unity/Assets/Resources/Art/StyleC/Sections'
$review = Join-Path $pack 'review'
New-Item -ItemType Directory -Force -Path $target,$review | Out-Null
$files = @(Get-ChildItem -LiteralPath (Join-Path $pack 'originals') -Filter '*.png' | Sort-Object Name)
if ($files.Count -ne 18) { throw 'Expected 18 originals' }
$manifest = foreach ($file in $files) {
 $src = [System.Drawing.Bitmap]::new($file.FullName)
 $bounds = [SectionAlpha]::Bounds($src)
 if ($bounds[5] -eq 0) { throw "Invalid alpha: $($file.Name)" }
 $left=[Math]::Max(0,$bounds[0]-4);$top=[Math]::Max(0,$bounds[1]-4)
 $crop=[System.Drawing.Rectangle]::new($left,$top,[Math]::Min($src.Width-1,$bounds[2]+4)-$left+1,[Math]::Min($src.Height-1,$bounds[3]+4)-$top+1)
 $ratio=512.0/[Math]::Max($crop.Width,$crop.Height)
 $width=[int][Math]::Round($crop.Width*$ratio);$height=[int][Math]::Round($crop.Height*$ratio)
 $dst=[System.Drawing.Bitmap]::new($width,$height,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
 $graphics=[System.Drawing.Graphics]::FromImage($dst)
 $graphics.CompositingMode=[System.Drawing.Drawing2D.CompositingMode]::SourceCopy
 $graphics.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
 $graphics.PixelOffsetMode=[System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
 $graphics.DrawImage($src,[System.Drawing.Rectangle]::new(0,0,$width,$height),$crop,[System.Drawing.GraphicsUnit]::Pixel)
 $dest=Join-Path $target $file.Name;$dst.Save($dest,[System.Drawing.Imaging.ImageFormat]::Png)
 [pscustomobject]@{id=$file.BaseName;originalSha256=(Get-FileHash -LiteralPath $file.FullName).Hash;originalWidth=$src.Width;originalHeight=$src.Height;alphaBounds=$bounds[0..3];originalNonzeroAlphaBorderPixels=$bounds[4];crop=@($crop.X,$crop.Y,$crop.Width,$crop.Height);runtimeWidth=$width;runtimeHeight=$height;runtimeSha256=(Get-FileHash -LiteralPath $dest).Hash}
 $graphics.Dispose();$dst.Dispose();$src.Dispose()
}
$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $pack 'manifest.json') -Encoding utf8
$sheet=[System.Drawing.Bitmap]::new(1500,1240)
$g=[System.Drawing.Graphics]::FromImage($sheet);$g.Clear([System.Drawing.ColorTranslator]::FromHtml('#183A4B'))
$g.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.TextRenderingHint=[System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
$font=[System.Drawing.Font]::new('Segoe UI',13)
for($i=0;$i -lt $files.Count;$i++) {
 $img=[System.Drawing.Image]::FromFile((Join-Path $target $files[$i].Name));$scale=[Math]::Min(250.0/$img.Width,260.0/$img.Height)
 $w=[int]($img.Width*$scale);$h=[int]($img.Height*$scale);$x=($i%5)*300;$y=[Math]::Floor($i/5)*310
 $g.DrawImage($img,[int]($x+(300-$w)/2),[int]($y+10+(260-$h)/2),$w,$h)
 $g.DrawString($files[$i].BaseName,$font,[System.Drawing.Brushes]::White,[single]($x+18),[single]($y+280));$img.Dispose()
}
$sheet.Save((Join-Path $review 'all-sections-navy.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$font.Dispose();$g.Dispose();$sheet.Dispose()
$manifest | Select-Object id,runtimeWidth,runtimeHeight,originalNonzeroAlphaBorderPixels | Format-Table
