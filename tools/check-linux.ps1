$ErrorActionPreference='Stop'
Push-Location (Split-Path $PSScriptRoot)
try {
    function Run([scriptblock]$Command) { & $Command; if($LASTEXITCODE -ne 0) {throw 'Local check failed.'} }
    Run { docker info --format '{{.OSType}}' }
    Run { docker build -t resttimer-local-ci -f tools/local-ci.Dockerfile tools }
    New-Item -ItemType Directory -Force build/local-ci-linux | Out-Null
    $source=(Get-Location).Path;$output=Join-Path $source 'build/local-ci-linux'
    if(git status --porcelain) {throw 'Commit changes before checking the exact source tree.'}
    $archive=Join-Path $source 'build/local-ci-source.tar'
    Run { git archive --format=tar --output=$archive HEAD }
    $runner=Join-Path $source 'tools/local-ci-linux.sh'
    Run { docker run --rm --mount "type=bind,source=$archive,target=/src.tar,readonly" --mount "type=bind,source=$runner,target=/runner.sh,readonly" --mount "type=bind,source=$output,target=/out" --mount type=volume,source=resttimer-local-pip,target=/root/.cache/pip resttimer-local-ci bash /runner.sh }
    Run { docker run --rm --mount "type=bind,source=$output,target=/packages,readonly" ubuntu:24.04 bash -ec 'apt-get update; DEBIAN_FRONTEND=noninteractive apt-get install -y /packages/release/*.deb; useradd -m smoke; runuser -u smoke -- env QT_QPA_PLATFORM=offscreen /opt/resttimer/resttimer --smoke-test; apt-get remove -y resttimer; test ! -e /opt/resttimer/resttimer' }
    Run { docker run --rm --mount "type=bind,source=$output,target=/packages,readonly" fedora:43 bash -ec 'dnf install -y util-linux /packages/release/*.rpm; useradd -m smoke; runuser -u smoke -- env QT_QPA_PLATFORM=offscreen /opt/resttimer/resttimer --smoke-test; dnf remove -y resttimer; test ! -e /opt/resttimer/resttimer' }
} finally { Pop-Location }
