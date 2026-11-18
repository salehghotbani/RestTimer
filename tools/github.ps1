function Invoke-RestTimerGh {
    param([Parameter(Mandatory=$true)][string[]]$Arguments)
    $oldHttp=$env:HTTP_PROXY;$oldHttps=$env:HTTPS_PROXY
    try {
        $settings=Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings'
        if($settings.ProxyEnable -and $settings.ProxyServer) {
            $proxy=$settings.ProxyServer
            if($proxy -match '(^|;)https=([^;]+)') {$proxy=$Matches[2]}
            elseif($proxy -match '(^|;)http=([^;]+)') {$proxy=$Matches[2]}
            if($proxy -notmatch '://') {$proxy='http://'+$proxy}
            $env:HTTP_PROXY=$env:HTTPS_PROXY=$proxy
        }
        & gh @Arguments
        if($LASTEXITCODE -ne 0){throw 'GitHub operation failed.'}
    } finally {$env:HTTP_PROXY=$oldHttp;$env:HTTPS_PROXY=$oldHttps}
}
