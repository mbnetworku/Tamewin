# Shared list of privacy settings used by Apply-Privacy.ps1 and Revert-Privacy.ps1.
# Only current-user (HKCU) settings. Does NOT touch Microsoft account / sign-in.
$cdm    = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager'
$search = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\SearchSettings'
$adv    = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced'
$offPol = 'HKCU:\Software\Policies\Microsoft\Office'

# Groups shown in the menu, in this order.
$PrivacyGroups = [ordered]@{
    Tips        = 'Tips, suggestions & ads'
    Search      = 'Search history & web search'
    Typing      = 'Feedback, speech & typing'
    Office      = 'Office telemetry'
    OfficeAdmin = 'Office connected experiences & feedback (needs Administrator)'
}

$PrivacySettings = @(
    # --- Windows: tips, suggestions, ads ---
    @{ Group='Tips'; Path=$cdm; Name='SubscribedContent-338389Enabled'; Value=0; Note='Tips and suggestions' }
    @{ Group='Tips'; Path=$cdm; Name='SubscribedContent-338393Enabled'; Value=0; Note='Suggested content in Settings (1)' }
    @{ Group='Tips'; Path=$cdm; Name='SubscribedContent-353694Enabled'; Value=0; Note='Suggested content in Settings (2)' }
    @{ Group='Tips'; Path=$cdm; Name='SubscribedContent-353696Enabled'; Value=0; Note='Suggested content in Settings (3)' }
    @{ Group='Tips'; Path=$cdm; Name='SubscribedContent-338388Enabled'; Value=0; Note='Suggestions in Start' }
    @{ Group='Tips'; Path=$cdm; Name='SystemPaneSuggestionsEnabled';    Value=0; Note='Suggestions in Start pane' }
    @{ Group='Tips'; Path=$cdm; Name='SoftLandingEnabled';              Value=0; Note='Tips about Windows' }
    @{ Group='Tips'; Path=$cdm; Name='SilentInstalledAppsEnabled';      Value=0; Note='Auto-install suggested apps' }
    @{ Group='Tips'; Path='HKCU:\Software\Microsoft\Windows\CurrentVersion\UserProfileEngagement'; Name='ScoobeSystemSettingEnabled'; Value=0; Note='"Finish setting up your device" prompts' }
    @{ Group='Tips'; Path='HKCU:\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo'; Name='Enabled'; Value=0; Note='Advertising ID' }
    @{ Group='Tips'; Path='HKCU:\Software\Microsoft\Windows\CurrentVersion\Privacy'; Name='TailoredExperiencesWithDiagnosticDataEnabled'; Value=0; Note='Tailored experiences' }
    @{ Group='Tips'; Path='HKCU:\Control Panel\International\User Profile'; Name='HttpAcceptLanguageOptOut'; Value=1; Note='Websites reading your language list' }
    @{ Group='Tips'; Path=$adv; Name='Start_TrackProgs';          Value=0; Note='Track app launches' }
    @{ Group='Tips'; Path=$adv; Name='Start_IrisRecommendations'; Value=0; Note='Recommendations in Start' }

    # --- Windows: search ---
    @{ Group='Search'; Path=$search; Name='IsDeviceSearchHistoryEnabled'; Value=0; Note='Search history on this device' }
    @{ Group='Search'; Path=$search; Name='IsMSACloudSearchEnabled';      Value=0; Note='Cloud content search (Microsoft account)' }
    @{ Group='Search'; Path=$search; Name='IsAADCloudSearchEnabled';      Value=0; Note='Cloud content search (work account)' }
    @{ Group='Search'; Path='HKCU:\Software\Microsoft\Windows\CurrentVersion\Search'; Name='BingSearchEnabled'; Value=0; Note='Bing web results in Start search' }

    # --- Windows: feedback, speech, typing ---
    @{ Group='Typing'; Path='HKCU:\Software\Microsoft\Siuf\Rules'; Name='NumberOfSIUFInPeriod'; Value=0; Note='Feedback requests' }
    @{ Group='Typing'; Path='HKCU:\Software\Microsoft\Speech_OneCore\Settings\OnlineSpeechPrivacy'; Name='HasAccepted'; Value=0; Note='Online speech recognition' }
    @{ Group='Typing'; Path='HKCU:\Software\Microsoft\InputPersonalization'; Name='RestrictImplicitTextCollection'; Value=1; Note='Typing data collection' }
    @{ Group='Typing'; Path='HKCU:\Software\Microsoft\InputPersonalization'; Name='RestrictImplicitInkCollection';  Value=1; Note='Handwriting data collection' }
    @{ Group='Typing'; Path='HKCU:\Software\Microsoft\InputPersonalization\TrainedDataStore'; Name='HarvestContacts'; Value=0; Note='Contacts used for typing' }
    @{ Group='Typing'; Path='HKCU:\Software\Microsoft\Input\TIPC'; Name='Enabled'; Value=0; Note='Improve inking & typing' }

    # --- Office (Word, Excel, ...) ---
    @{ Group='Office'; Path='HKCU:\Software\Microsoft\Office\Common\ClientTelemetry'; Name='DisableTelemetry'; Value=1; Note='Office telemetry' }
    @{ Group='Office'; Path='HKCU:\Software\Microsoft\Office\Common\ClientTelemetry'; Name='SendTelemetry';    Value=3; Note='Office diagnostic data: neither' }
    @{ Group='OfficeAdmin'; Path="$offPol\Common\ClientTelemetry"; Name='SendTelemetry'; Value=3; Note='Office diagnostic data: neither (policy)' }
    @{ Group='OfficeAdmin'; Path="$offPol\16.0\Common\Privacy"; Name='usercontentdisabled';                Value=2; Note='Office: experiences that analyze your content' }
    @{ Group='OfficeAdmin'; Path="$offPol\16.0\Common\Privacy"; Name='downloadcontentdisabled';            Value=2; Note='Office: experiences that download online content' }
    @{ Group='OfficeAdmin'; Path="$offPol\16.0\Common\Privacy"; Name='controllerconnectedservicesenabled'; Value=2; Note='Office: optional connected experiences' }
    @{ Group='OfficeAdmin'; Path="$offPol\16.0\Common\Feedback"; Name='enabled';           Value=0; Note='Office: Send feedback' }
    @{ Group='OfficeAdmin'; Path="$offPol\16.0\Common\Feedback"; Name='includescreenshot'; Value=0; Note='Office: screenshots in feedback' }
    @{ Group='OfficeAdmin'; Path="$offPol\16.0\Common\Feedback"; Name='surveyenabled';     Value=0; Note='Office: surveys' }
)

$BackupFile = Join-Path $PSScriptRoot 'privacy-backup.json'

# Turns "1,3,5-7" into unique numbers between 1 and $Max.
function ConvertTo-Numbers([string]$Text, [int]$Max) {
    $nums = foreach ($part in ($Text -split '[,\s]+' | Where-Object { $_ })) {
        if ($part -match '^(\d+)-(\d+)$') { [int]$Matches[1]..[int]$Matches[2] }
        elseif ($part -match '^\d+$')     { [int]$part }
        else { Write-Host "Ignoring '$part'" -ForegroundColor Yellow }
    }
    $nums | Where-Object { $_ -ge 1 -and $_ -le $Max } | Sort-Object -Unique
}

# Returns the settings to act on: from -All / -Group, or from an interactive menu.
function Select-PrivacySettings {
    param([string]$Action, [string[]]$Group, [switch]$All)

    if ($All) { return $PrivacySettings }
    if ($Group) {
        $unknown = $Group | Where-Object { -not $PrivacyGroups.Contains($_) }
        if ($unknown) { throw "Unknown group: $($unknown -join ', '). Valid groups: $($PrivacyGroups.Keys -join ', ')" }
        return $PrivacySettings | Where-Object { $Group -contains $_.Group }
    }

    $keys = @($PrivacyGroups.Keys)
    Write-Host ""
    Write-Host "Choose what to ${Action}:"
    for ($i = 0; $i -lt $keys.Count; $i++) {
        $count = @($PrivacySettings | Where-Object { $_.Group -eq $keys[$i] }).Count
        Write-Host ("  {0}) {1}  [{2} settings]" -f ($i + 1), $PrivacyGroups[$keys[$i]], $count)
    }
    Write-Host "  A) All"
    Write-Host "  I) Pick individual settings"
    Write-Host "  Q) Quit"
    $answer = "$(Read-Host 'Your choice (e.g. 1,3)')".Trim()

    switch -Regex ($answer) {
        '^[Qq]?$' { return @() }
        '^[Aa]$'  { return $PrivacySettings }
        '^[Ii]$'  {
            for ($i = 0; $i -lt $PrivacySettings.Count; $i++) {
                Write-Host ("  {0,2}) {1}" -f ($i + 1), $PrivacySettings[$i].Note)
            }
            $nums = ConvertTo-Numbers (Read-Host 'Numbers (e.g. 1,4,7-9)') $PrivacySettings.Count
            return $nums | ForEach-Object { $PrivacySettings[$_ - 1] }
        }
        default {
            $chosen = ConvertTo-Numbers $answer $keys.Count | ForEach-Object { $keys[$_ - 1] }
            return $PrivacySettings | Where-Object { $chosen -contains $_.Group }
        }
    }
}
