export const environment = {
  APP_NAME: 'WanesCP',
  // Plain HTTP on the  host's LAN address, so the CMS works both from this PC
  // and from any other machine on the Wi-Fi. The backend binds 0.0.0.0 (see
  // backend/Wanes/Properties/launchSettings.json); :5001 is skipped because its
  // self-signed dev cert makes the browser reject the XHR. Keep in sync with
  // Environment.apiBaseUrl in the Flutter app.
  apiBaseUrl: 'http://192.168.10.150:5000/api/v1/',
};
