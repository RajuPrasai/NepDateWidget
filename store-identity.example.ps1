# store-identity.example.ps1 - committed template
# Copy this file to store-identity.ps1, fill in real values from Partner Center,
# and never commit that copy (it is listed in .gitignore).
#
# HOW TO GET THESE VALUES
#   partner.microsoft.com/dashboard
#     → Windows & Xbox → your app → Product management → Product identity
#
#   PackageName          = Package/Identity/Name
#   Publisher            = Package/Identity/Publisher   (the full CN=... string)
#   PublisherDisplayName = Package/Properties/PublisherDisplayName

$PackageName          = 'YourReservedPackageName'
$Publisher            = 'CN=YourPublisherString'
$PublisherDisplayName = 'Your Publisher Display Name'
