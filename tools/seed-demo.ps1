<#
  Seeds demo data (drivers, riders, vehicles, trips, bookings, ride requests)
  through the public API, so every row passes the same business rules the apps do.

  Requires: backend running with Otp:IsTesting=true (fixed code 1234).
  Usage:    powershell -File tools/seed-demo.ps1 [-Api http://localhost:5000/api/v1]

  Users are keyed by phone, so re-running reuses them; trips, bookings and
  requests are created again on every run.
#>
param([string]$Api = "http://localhost:5000/api/v1")

$ErrorActionPreference = "Stop"
$OtpCode = "1234"
$AdminPhone = "+962790000000"

function Invoke-Api {
    param([string]$Method, [string]$Path, $Body, [string]$Token, [switch]$Quiet)
    $headers = @{ "Accept-Language" = "en" }
    if ($Token) { $headers["Authorization"] = "Bearer $Token" }
    $params = @{ Method = $Method; Uri = "$Api/$Path"; Headers = $headers; ContentType = "application/json; charset=utf-8" }
    if ($null -ne $Body) { $params.Body = [Text.Encoding]::UTF8.GetBytes(($Body | ConvertTo-Json -Depth 6)) }
    $res = Invoke-RestMethod @params
    if (-not $res.success -and -not $Quiet) {
        Write-Warning "$Method $Path failed: errorCode=$($res.errorCode) $($res.message) $($res.errors -join '; ')"
    }
    return $res
}

function Connect-User([string]$Phone) {
    Invoke-Api POST "accounts/request-otp" @{ phone = $Phone } | Out-Null
    $auth = Invoke-Api POST "accounts/verify-otp" @{ phone = $Phone; code = $OtpCode; deviceType = 1 }
    return $auth.data.token
}

function Point($name, $lat, $lng) { @{ lat = $lat; lng = $lng; address = $name } }
function At([int]$hoursFromNow) { (Get-Date).ToUniversalTime().AddHours($hoursFromNow).ToString("yyyy-MM-ddTHH:mm:ssZ") }

# ── Places (Jordan) ──
$P = @{
    Abdali     = Point "Abdali Boulevard, Amman"          31.9632 35.9106
    Sweifieh   = Point "Sweifieh, Amman"                  31.9570 35.8620
    UJ         = Point "University of Jordan, Amman"      32.0142 35.8727
    Khalda     = Point "Khalda, Amman"                    31.9990 35.8370
    Zarqa      = Point "Zarqa City Centre"                32.0728 36.0880
    Irbid      = Point "Yarmouk University, Irbid"        32.5364 35.8561
    Salt       = Point "Salt Downtown"                    32.0392 35.7272
    Madaba     = Point "Madaba"                           31.7167 35.8000
    Airport    = Point "Queen Alia International Airport" 31.7226 35.9932
    DeadSea    = Point "Dead Sea Resorts"                 31.7159 35.5890
    Aqaba      = Point "Aqaba"                            29.5321 35.0063
    Jerash     = Point "Jerash"                           32.2747 35.8961
}

# ── People ── gender: 1 male, 2 female
$drivers = @(
    @{ key = "omar";   phone = "+962791000001"; first = "Omar";   last = "Haddad";   gender = 1; dob = "1988-04-12"; license = "JO-DL-100231"; car = @{ make = "Toyota";  model = "Camry";   plate = "10-45321"; color = "White";  year = 2021; seatCapacity = 4 } }
    @{ key = "lina";   phone = "+962791000002"; first = "Lina";   last = "Khalil";   gender = 2; dob = "1992-09-03"; license = "JO-DL-100232"; car = @{ make = "Hyundai"; model = "Tucson";  plate = "12-77810"; color = "Silver"; year = 2022; seatCapacity = 4 } }
    @{ key = "yousef"; phone = "+962791000003"; first = "Yousef"; last = "Nasser";   gender = 1; dob = "1985-01-25"; license = "JO-DL-100233"; car = @{ make = "Kia";     model = "Carnival";plate = "20-11873"; color = "Black";  year = 2020; seatCapacity = 7 } }
    @{ key = "rania";  phone = "+962791000004"; first = "Rania";  last = "Saleh";    gender = 2; dob = "1995-06-18"; license = "JO-DL-100234"; car = @{ make = "Toyota";  model = "Prius";   plate = "13-90412"; color = "Blue";   year = 2019; seatCapacity = 4 } }
    @{ key = "khaled"; phone = "+962791000005"; first = "Khaled"; last = "Mansour";  gender = 1; dob = "1990-11-30"; license = "JO-DL-100235"; car = @{ make = "Mitsubishi"; model = "Xpander"; plate = "22-30588"; color = "Grey"; year = 2023; seatCapacity = 6 }; pending = $true }
)
$riders = @(
    @{ key = "sara";    phone = "+962782000001"; first = "Sara";    last = "Ahmad";    gender = 2; dob = "2001-02-14" }
    @{ key = "ahmad";   phone = "+962782000002"; first = "Ahmad";   last = "Zoubi";    gender = 1; dob = "1999-07-07" }
    @{ key = "noor";    phone = "+962782000003"; first = "Noor";    last = "Hamdan";   gender = 2; dob = "2003-12-01" }
    @{ key = "mohammad";phone = "+962782000004"; first = "Mohammad";last = "Obeidat";  gender = 1; dob = "1997-03-22" }
    @{ key = "hala";    phone = "+962782000005"; first = "Hala";    last = "Qasem";    gender = 2; dob = "1994-08-09" }
    @{ key = "tariq";   phone = "+962782000006"; first = "Tariq";   last = "Shami";    gender = 1; dob = "2000-05-16" }
    @{ key = "dana";    phone = "+962782000007"; first = "Dana";    last = "Majali";   gender = 2; dob = "1998-10-27" }
    @{ key = "fadi";    phone = "+962782000008"; first = "Fadi";    last = "Rawashdeh";gender = 1; dob = "1996-01-05" }
)

Write-Host "== Admin sign-in"
$adminToken = Connect-User $AdminPhone

$T = @{}   # key -> token
$U = @{}   # key -> user id
$V = @{}   # driver key -> vehicle id

function Set-Profile($person) {
    $tok = Connect-User $person.phone
    $me = Invoke-Api PATCH "accounts/me" @{
        firstName = $person.first; lastName = $person.last; displayName = $person.first
        gender = $person.gender; dateOfBirth = "$($person.dob)T00:00:00Z"
    } -Token $tok
    $T[$person.key] = $tok
    $U[$person.key] = $me.data.id
    Write-Host ("   {0,-9} id={1,-4} {2}" -f $person.first, $me.data.id, $person.phone)
}

Write-Host "== Riders"
foreach ($r in $riders) { Set-Profile $r }

Write-Host "== Drivers (apply, add vehicle, admin verification)"
foreach ($d in $drivers) {
    Set-Profile $d
    $tok = $T[$d.key]
    Invoke-Api POST "me/driver/apply" @{ licenseNumber = $d.license } -Token $tok -Quiet | Out-Null

    $existing = (Invoke-Api GET "me/vehicles" -Token $tok).data | Where-Object { $_.plate -eq $d.car.plate } | Select-Object -First 1
    if (-not $existing) {
        $car = $d.car.Clone(); $car.isDefault = $true
        $existing = (Invoke-Api POST "me/vehicles" $car -Token $tok).data
    }
    $V[$d.key] = $existing.id

    if (-not $d.pending) {
        Invoke-Api POST "admin/drivers/$($U[$d.key])/verify" @{ approve = $true; note = "Demo data - documents checked" } -Token $adminToken -Quiet | Out-Null
    } else {
        Write-Host "   $($d.first) left pending verification (shows in the CMS queue)"
    }
    Invoke-Api PATCH "accounts/me/role" @{ activeRole = 2 } -Token $tok -Quiet | Out-Null
}

Write-Host "== Trips"
function New-Trip($driver, $from, $to, $hours, $seats, $price, $minSeats = 0, $genderPolicy = 0) {
    $res = Invoke-Api POST "trips" @{
        vehicleId = $V[$driver]; origin = $from; destination = $to; departAt = (At $hours)
        seatsTotal = $seats; pricePerSeat = $price; minSeatsToConfirm = $minSeats; genderPolicy = $genderPolicy
    } -Token $T[$driver]
    if ($res.success) { Write-Host ("   #{0,-4} {1} -> {2}  in {3}h  seats {4}  {5} JOD" -f $res.data.id, $from.address, $to.address, $hours, $seats, $price) }
    return $res.data
}

$trips = [ordered]@{
    t1 = New-Trip "omar"   $P.Sweifieh $P.UJ      2   3 1.50
    t2 = New-Trip "omar"   $P.UJ       $P.Irbid   26  4 3.00 2
    t3 = New-Trip "lina"   $P.Khalda   $P.Abdali  3   3 1.25 0 2
    t4 = New-Trip "lina"   $P.Abdali   $P.DeadSea 50  4 5.00 3
    t5 = New-Trip "yousef" $P.Abdali   $P.Airport 5   6 4.00
    t6 = New-Trip "yousef" $P.Abdali   $P.Aqaba   72  6 12.00 3
    t7 = New-Trip "rania"  $P.Sweifieh $P.Salt    4   3 2.00
    t8 = New-Trip "rania"  $P.Zarqa    $P.Abdali  20  4 1.75
    t9 = New-Trip "omar"   $P.Abdali   $P.Madaba  1   3 2.50
}

Write-Host "== Bookings"
function Book($rider, $trip, $seats = 1) {
    if (-not $trip) { return }
    $res = Invoke-Api POST "bookings" @{ tripId = $trip.id; seats = $seats; acceptSharedRide = $true } -Token $T[$rider]
    if ($res.success) { Write-Host "   $rider booked $seats seat(s) on trip #$($trip.id)" }
}
Book "sara"     $trips.t1
Book "ahmad"    $trips.t1
Book "noor"     $trips.t3
Book "hala"     $trips.t3
Book "mohammad" $trips.t2 2
Book "tariq"    $trips.t5
Book "fadi"     $trips.t5 2
Book "dana"     $trips.t7
Book "ahmad"    $trips.t6
Book "sara"     $trips.t9
Book "tariq"    $trips.t9

Write-Host "== Trip #$($trips.t9.id) runs to completion (depart -> arrive -> start -> complete)"
if ($trips.t9) {
    foreach ($step in "depart","arrive","start","complete") {
        $r = Invoke-Api POST "trips/$($trips.t9.id)/$step" -Token $T["omar"]
        if ($r.success) { Write-Host "   $step -> status $($r.data.status)" }
    }
}

Write-Host "== Ride requests (demand)"
function New-Request($rider, $from, $to, $hours, $seats = 1, $driverGender = 0) {
    $res = Invoke-Api POST "ride-requests" @{
        origin = $from; destination = $to; departAt = (At $hours); seats = $seats
        nearby = $false; driverGenderPolicy = $driverGender; coRiderGenderPolicy = 0
        acceptSharedRide = $true
    } -Token $T[$rider]
    if ($res.success) { Write-Host ("   #{0,-4} {1}: {2} -> {3} in {4}h" -f $res.data.id, $rider, $from.address, $to.address, $hours) }
    return $res.data
}
$r1 = New-Request "noor"     $P.UJ      $P.Jerash   30 1 2
$r2 = New-Request "mohammad" $P.Abdali  $P.Zarqa    28 2
$r3 = New-Request "dana"     $P.Khalda  $P.Airport  36 1
$r4 = New-Request "fadi"     $P.Salt    $P.Abdali   40 1

if ($r2) {
    $j = Invoke-Api POST "ride-requests/$($r2.id)/join" @{ seats = 1; acceptSharedRide = $true } -Token $T["hala"]
    if ($j.success) { Write-Host "   hala joined request #$($r2.id)" }
}
if ($r4) {
    # Planned work: offers are collected, and riders may compare them.
    $o = Invoke-Api POST "ride-requests/$($r4.id)/interest" @{ vehicleId = $V["yousef"]; pricePerSeat = 2.25; acceptSharedTrip = $true } -Token $T["yousef"]
    if ($o.success) { Write-Host "   yousef offered on request #$($r4.id) -> status $($o.data.status), decides $($o.data.decideAt)" }
    $o2 = Invoke-Api POST "ride-requests/$($r4.id)/interest" @{ vehicleId = $V["rania"]; pricePerSeat = 1.75; acceptSharedTrip = $true; minPassengers = 2 } -Token $T["rania"]
    if ($o2.success) { Write-Host "   rania made a conditional offer (runs at 2) on request #$($r4.id)" }
}
if ($r3) {
    # Lina watches Dana's airport request and will hear when it reaches 2.
    $w = Invoke-Api POST "me/demand-alerts" @{ rideRequestId = $r3.id; minSeats = 2 } -Token $T["lina"]
    if ($w.success) { Write-Host "   lina is watching request #$($r3.id) for 2 passengers" }
}

Write-Host "== Route alerts, reliability, safety"
$alert = Invoke-Api POST "me/demand-alerts" @{
    origin = $P.Abdali; destination = $P.Zarqa; radiusMeters = 3000; minSeats = 3
} -Token $T["omar"]
if ($alert.success) { Write-Host "   omar alerts on Abdali -> Zarqa at 3+ seats" }

# Rania walks away from a trip Dana booked: a counted cancellation with a reason.
if ($trips.t7) {
    $preview = Invoke-Api GET "trips/$($trips.t7.id)/cancel-preview" -Token $T["rania"]
    $c = Invoke-Api POST "trips/$($trips.t7.id)/cancel" @{ reason = 2; note = "Flat tyre" } -Token $T["rania"]
    # Seconds after publishing, so this lands inside the free grace period;
    # a later cancellation of a booked trip would count and be flagged for review.
    $kinds = @{ 1 = "free (grace period)"; 2 = "counted"; 3 = "late" }
    if ($c.success) { Write-Host "   rania cancelled trip #$($trips.t7.id) (car problem) - recorded as $($kinds[[int]$preview.data.kind])" }
}

# Tariq files a safety report about the trip he took.
if ($trips.t5) {
    $s = Invoke-Api POST "safety/incidents" @{ kind = 2; tripId = $trips.t5.id; lat = 31.9632; lng = 35.9106; note = "Driver was on the phone while driving." } -Token $T["tariq"]
    if ($s.success) { Write-Host "   tariq reported a safety concern on trip #$($trips.t5.id)" }
}

Write-Host ""
Write-Host "Done. Every demo account signs in with OTP $OtpCode."
Write-Host "Drivers: $(($drivers | ForEach-Object { "$($_.first) $($_.phone)" }) -join ', ')"
Write-Host "Riders:  $(($riders  | ForEach-Object { "$($_.first) $($_.phone)" }) -join ', ')"
