$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @'
public static class ArtBounds {
 public static int[] Scan(byte[] b,int w,int h,int stride){int l=w,t=h,r=-1,d=-1;for(int y=0;y<h;y++)for(int x=0;x<w;x++)if(b[y*stride+x*4+3]>8){l=System.Math.Min(l,x);t=System.Math.Min(t,y);r=System.Math.Max(r,x);d=System.Math.Max(d,y);}if(r<0)throw new System.Exception("Empty artwork");return new[]{l,t,r-l+1,d-t+1};}
}
'@
$v3='X:/PROJECTS/FINAL-SVALKA/art_source/jtn/2026-11-15-style-c-colour-v003'
$support='X:/PROJECTS/FINAL-SVALKA/art_source/jtn/2026-11-15-style-c-unity-support-v005'
$target='X:/PROJECTS/FINAL-SVALKA/Unity/Assets/Resources/Art/StyleC'
foreach($folder in @('Houses','Symbols','Icons','Buttons','Sites')){New-Item -ItemType Directory -Path (Join-Path $target $folder) -Force | Out-Null}
function Bounds($image){$d=$image.LockBits([System.Drawing.Rectangle]::new(0,0,$image.Width,$image.Height),[System.Drawing.Imaging.ImageLockMode]::ReadOnly,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb);try{$b=[byte[]]::new($d.Stride*$image.Height);[System.Runtime.InteropServices.Marshal]::Copy($d.Scan0,$b,0,$b.Length);return [ArtBounds]::Scan($b,$image.Width,$image.Height,$d.Stride)}finally{$image.UnlockBits($d)}}
function Export-Crop($image,$bounds,$path){$x=[math]::Max(0,$bounds[0]-3);$y=[math]::Max(0,$bounds[1]-3);$r=[math]::Min($image.Width,$bounds[0]+$bounds[2]+3);$d=[math]::Min($image.Height,$bounds[1]+$bounds[3]+3);$crop=$image.Clone([System.Drawing.Rectangle]::new($x,$y,$r-$x,$d-$y),[System.Drawing.Imaging.PixelFormat]::Format32bppArgb);try{$crop.Save($path,[System.Drawing.Imaging.ImageFormat]::Png)}finally{$crop.Dispose()}}
foreach($folder in @('Symbols','Icons','Buttons')){
 foreach($file in (Get-ChildItem -LiteralPath (Join-Path $v3 ('exports/'+$folder.ToLowerInvariant())) -Filter '*.png')){
  $image=[System.Drawing.Bitmap]::new($file.FullName);try{Export-Crop $image (Bounds $image) (Join-Path $target ($folder+'/'+$file.Name))}finally{$image.Dispose()}
 }
}
Get-ChildItem -LiteralPath (Join-Path $v3 'exports/houses') -Filter '*.png' | ForEach-Object{Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $target ('Houses/'+$_.Name))}
Copy-Item -LiteralPath (Join-Path $support 'originals/courtyard-background.png') -Destination (Join-Path $target 'courtyard-background.png')
$atlas=[System.Drawing.Bitmap]::new((Join-Path $support 'originals/initial-sites-clean.png'))
try{
 $ids=@('workshop','recycler','loft','cart','power')
 for($i=0;$i -lt 5;$i++){
  $cell=$atlas.Clone([System.Drawing.Rectangle]::new(($i%3*512),([int][math]::Floor($i/3)*512),512,512),[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  try{Export-Crop $cell (Bounds $cell) (Join-Path $target ('Sites/'+$ids[$i]+'.png'))}finally{$cell.Dispose()}
 }
}finally{$atlas.Dispose()}
Write-Output (@(Get-ChildItem -LiteralPath $target -Recurse -Filter '*.png').Count.ToString()+' runtime PNGs prepared')
