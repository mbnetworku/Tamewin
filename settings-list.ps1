# Shared list of privacy settings used by Apply-Privacy.ps1 and Revert-Privacy.ps1.
# Only current-user (HKCU) settings. Does NOT touch Microsoft account / sign-in.
$cdm    = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager'
$search = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\SearchSettings'
$adv    = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced'
$offPol = 'HKCU:\Software\Policies\Microsoft\Office'

$PrivacySettings = @(
    # --- Windows: tips, suggestions, ads ---
    @{ Path=$cdm; Name='SubscribedContent-338389Enabled'; Value=0; Note='Tips and suggestions' }
    @{ Path=$cdm; Name='SubscribedContent-338393Enabled'; Value=0; Note='Suggested content in Settings' }
    @{ Path=$cdm; Name='SubscribedContent-353694Enabled'; Value=0; Note='Suggested content in Settings' }
    @{ Path=$cdm; Name='SubscribedContent-353696Enabled'; Value=0; Note='Suggested content in Settings' }
    @{ Path=$cdm; Name='SubscribedContent-338388Enabled'; Value=0; Note='Suggestions in Start' }
    @{ Path=$cdm; Name='SystemPaneSuggestionsEnabled';    Value=0; Note='Suggestions in Start' }
    @{ Path=$cdm; Name='SoftLandingEnabled';              Value=0; Note='Tips about Windows' }
    @{ Path=$cdm; Name='SilentInstalledAppsEnabled';      Value=0; Note='Auto-install suggested apps' }
    @{ Path='HKCU:\Software\Microsoft\Windows\CurrentVersion\UserProfileEngagement'; Name='ScoobeSystemSettingEnabled'; Value=0; Note='"Finish setting up your device" prompts' }
    @{ Path='HKCU:\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo'; Name='Enabled'; Value=0; Note='Advertising ID' }
    @{ Path='HKCU:\Software\Microsoft\Windows\CurrentVersion\Privacy'; Name='TailoredExperiencesWithDiagnosticDataEnabled'; Value=0; Note='Tailored experiences' }
    @{ Path='HKCU:\Control Panel\International\User Profile'; Name='HttpAcceptLanguageOptOut'; Value=1; Note='Websites reading your language list' }
    @{ Path=$adv; Name='Start_TrackProgs';          Value=0; Note='Track app launches' }
    @{ Path=$adv; Name='Start_IrisRecommendations'; Value=0; Note='Recommendations in Start' }

    # --- Windows: search ---
    @{ Path=$search; Name='IsDeviceSearchHistoryEnabled'; Value=0; Note='Search history on this device' }
    @{ Path=$search; Name='IsMSACloudSearchEnabled';      Value=0; Note='Cloud content search (Microsoft account)' }
    @{ Path=$search; Name='IsAADCloudSearchEnabled';      Value=0; Note='Cloud content search (work account)' }
    @{ Path='HKCU:\Software\Microsoft\Windows\CurrentVersion\Search'; Name='BingSearchEnabled'; Value=0; Note='Bing web results in Start search' }

    # --- Windows: feedback, speech, typing ---
    @{ Path='HKCU:\Software\Microsoft\Siuf\Rules'; Name='NumberOfSIUFInPeriod'; Value=0; Note='Feedback requests' }
    @{ Path='HKCU:\Software\Microsoft\Speech_OneCore\Settings\OnlineSpeechPrivacy'; Name='HasAccepted'; Value=0; Note='Online speech recognition' }
    @{ Path='HKCU:\Software\Microsoft\InputPersonalization'; Name='RestrictImplicitTextCollection'; Value=1; Note='Typing data collection' }
    @{ Path='HKCU:\Software\Microsoft\InputPersonalization'; Name='RestrictImplicitInkCollection';  Value=1; Note='Handwriting data collection' }
    @{ Path='HKCU:\Software\Microsoft\InputPersonalization\TrainedDataStore'; Name='HarvestContacts'; Value=0; Note='Contacts used for typing' }
    @{ Path='HKCU:\Software\Microsoft\Input\TIPC'; Name='Enabled'; Value=0; Note='Improve inking & typing' }

    # --- Office (Word, Excel, ...) ---
    @{ Path='HKCU:\Software\Microsoft\Office\Common\ClientTelemetry'; Name='DisableTelemetry'; Value=1; Note='Office telemetry' }
    @{ Path='HKCU:\Software\Microsoft\Office\Common\ClientTelemetry'; Name='SendTelemetry';    Value=3; Note='Office diagnostic data: neither' }
    @{ Path="$offPol\Common\ClientTelemetry"; Name='SendTelemetry'; Value=3; Note='Office diagnostic data: neither (policy)' }
    @{ Path="$offPol\16.0\Common\Privacy"; Name='usercontentdisabled';                Value=2; Note='Office: experiences that analyze your content' }
    @{ Path="$offPol\16.0\Common\Privacy"; Name='downloadcontentdisabled';            Value=2; Note='Office: experiences that download online content' }
    @{ Path="$offPol\16.0\Common\Privacy"; Name='controllerconnectedservicesenabled'; Value=2; Note='Office: optional connected experiences' }
    @{ Path="$offPol\16.0\Common\Feedback"; Name='enabled';           Value=0; Note='Office: Send feedback' }
    @{ Path="$offPol\16.0\Common\Feedback"; Name='includescreenshot'; Value=0; Note='Office: screenshots in feedback' }
    @{ Path="$offPol\16.0\Common\Feedback"; Name='surveyenabled';     Value=0; Note='Office: surveys' }
)

$BackupFile = Join-Path $PSScriptRoot 'privacy-backup.json'
