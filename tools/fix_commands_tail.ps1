# fix_commands_tail.ps1 - Complete the truncated commands file
$commandsFile = "c:\Users\ahmad\OneDrive\Desktop\BL\Commands\CastlePolicyCommands.cs"
$bytes = [System.IO.File]::ReadAllBytes($commandsFile)
$text = [System.Text.Encoding]::UTF8.GetString($bytes)

# Find the truncated "  .castlepolicy.allow ..." line
$truncatedPattern = '               "  .castlepolicy.allow <policyId> <steamId> \[name\]\\n" \+'
$tailToAppend = @"
               "  .castlepolicy.deny <policyId> <steamId> [name]" + Environment.NewLine +
               "  .castlepolicy.territory.preview <public|private>" + Environment.NewLine +
               "  .castlepolicy.territory.apply <public|private> confirm" + Environment.NewLine +
               "  .castlepolicy.remove <policyId>" + Environment.NewLine +
               "  .castlepolicy.excluded <policyId> <true|false>  (mark a policy as excluded from territory apply)" + Environment.NewLine;
    }
}
"@

if ($text -match '               "  \.castlepolicy\.allow <policyId> <steamId> \[name\]\\n" \+') {
    $text = $text -replace '               "  \.castlepolicy\.allow <policyId> <steamId> \[name\]\\n" \+', $tailToAppend
    Write-Host "Replaced truncated tail"
} else {
    Write-Host "Pattern not found, appending tail at end"
    $text = $text.TrimEnd() + "`r`n" + $tailToAppend
}

[System.IO.File]::WriteAllText($commandsFile, $text, [System.Text.UTF8Encoding]::new($false))
Write-Host "Done"
