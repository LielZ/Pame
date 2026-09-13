$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$destination=Join-Path $root 'src/Pame.App/Assets/Prompts'
New-Item -ItemType Directory -Path $destination -Force | Out-Null
$maps=@{
 Xbox=@{Folder='Xbox Series';confirm='xbox_button_a';back='xbox_button_b';search='xbox_button_y';favorite='xbox_button_x';previous='xbox_lb';next='xbox_rb';menu='xbox_button_menu';device='controller_xboxseries'}
 PlayStation=@{Folder='PlayStation Series';confirm='playstation_button_cross';back='playstation_button_circle';search='playstation_button_triangle';favorite='playstation_button_square';previous='playstation_trigger_l1';next='playstation_trigger_r1';menu='playstation5_button_options';device='controller_playstation5'}
 Switch=@{Folder='Nintendo Switch';confirm='switch_button_b';back='switch_button_a';search='switch_button_x';favorite='switch_button_y';previous='switch_button_l';next='switch_button_r';menu='switch_button_plus';device='controller_switch_pro'}
}
$sources=@()
foreach($family in $maps.Keys){
 $map=$maps[$family]
 foreach($action in $map.Keys | Where-Object {$_ -ne 'Folder'}){
  $source=Join-Path $root ('research/ui-assets/kenney/'+$map.Folder+'/Double/'+$map[$action]+'.png')
  Copy-Item -LiteralPath $source -Destination (Join-Path $destination ($family+'-'+$action+'.png'))
  $sources+=@{File=('Prompts/'+$family+'-'+$action+'.png');Source=('Kenney Input Prompts 1.5A/'+$map.Folder+'/Double/'+$map[$action]+'.png')}
 }
}
Copy-Item -LiteralPath (Join-Path $root 'research/ui-assets/kenney/License.txt') -Destination (Join-Path $root 'licenses/KenneyInputPrompts.txt')
$sources | ConvertTo-Json | Set-Content (Join-Path $root 'docs/INPUT_ASSET_SOURCES.json')
