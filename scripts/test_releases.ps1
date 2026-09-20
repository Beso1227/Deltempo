$client = [System.Net.Http.HttpClient]::new()
$client.DefaultRequestHeaders.UserAgent.ParseAdd('Deltempo-Updater')
$json = $client.GetStringAsync('https://api.github.com/repos/Beso1227/Deltempo/releases').GetAwaiter().GetResult()
$doc = [System.Text.Json.JsonDocument]::Parse($json)
foreach ($r in $doc.RootElement.EnumerateArray()) {
    $tag = $r.GetProperty('tag_name').GetString()
    $name = $r.GetProperty('name').GetString()
    $draft = $r.GetProperty('draft').GetBoolean()
    $pre = $r.GetProperty('prerelease').GetBoolean()
    $assets = $r.GetProperty('assets').GetArrayLength()
    Write-Host "Tag: $tag | Name: $name | Draft: $draft | Pre: $pre | Assets: $assets"
    foreach ($a in $r.GetProperty('assets').EnumerateArray()) {
        $an = $a.GetProperty('name').GetString()
        $url = $a.GetProperty('browser_download_url').GetString()
        $sz = $a.GetProperty('size').GetInt64()
        Write-Host "   Asset: $an ($sz bytes) -> $url"
    }
}
