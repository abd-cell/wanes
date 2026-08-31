import Flutter
import UIKit
import UserNotifications

@main
@objc class AppDelegate: FlutterAppDelegate, FlutterImplicitEngineDelegate {
  override func application(
    _ application: UIApplication,
    didFinishLaunchingWithOptions launchOptions: [UIApplication.LaunchOptionsKey: Any]?
  ) -> Bool {
    // Required by firebase_messaging and flutter_local_notifications: without a
    // delegate set here, iOS never surfaces a foreground notification and the
    // APNs token never reaches FCM.
    UNUserNotificationCenter.current().delegate = self

    // Registers for APNs. FCM swizzles this to pick up the device token; the
    // user is not prompted by this call — PushService.requestPermission does
    // that after sign-in.
    application.registerForRemoteNotifications()

    return super.application(application, didFinishLaunchingWithOptions: launchOptions)
  }

  func didInitializeImplicitFlutterEngine(_ engineBridge: FlutterImplicitEngineBridge) {
    GeneratedPluginRegistrant.register(with: engineBridge.pluginRegistry)
  }
}
