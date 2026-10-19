import 'package:device_preview/device_preview.dart';
import 'package:flutter/foundation.dart' show kReleaseMode;
import 'package:flutter/gestures.dart';
import 'package:flutter/material.dart';
import 'package:get/get.dart';
import 'package:oasx/service/profile_service.dart';
import 'package:oasx/config/theme.dart'
    show amoledDarkTheme, darkTheme, lightTheme;
import 'package:oasx/modules/settings/controllers/settings_controller.dart';
import 'package:oasx/service/autostart_service.dart';
import 'package:oasx/service/app_update_service.dart';
import 'package:oasx/service/locale_service.dart';
import 'package:oasx/service/script_service.dart';
import 'package:oasx/service/system_tray_service.dart';
import 'package:oasx/service/theme_service.dart';
import 'package:oasx/service/websocket_service.dart';
import 'package:oasx/service/window_service.dart';
import 'package:oasx/service/acrylic_service.dart';
import 'package:oasx/translation/i18n.dart';
import 'package:oasx/utils/logger.dart';
import 'package:oasx/utils/platform_utils.dart';
import 'package:oasx/routes.dart';
import 'package:responsive_builder/responsive_builder.dart';

void main() async {
  WidgetsFlutterBinding.ensureInitialized();
  await initService();

  runApp(
    DevicePreview(
      // DevicePreview 的模拟外壳与窗口透明冲突，亚克力模式下禁用
          enabled: !kReleaseMode &&
              PlatformUtils.isWindows && !AcrylicService.transparentBackground,
      builder: (context) => const OASXApp(),
    ),
  );
}

class OASXApp extends StatelessWidget {
  const OASXApp({super.key});

  @override
  Widget build(BuildContext context) {
    final localeService = Get.find<LocaleService>();

    return ResponsiveApp(
      builder: (context) {
        return GetMaterialApp(
          debugShowCheckedModeBanner: false,
          builder: DevicePreview.appBuilder,
          scrollBehavior: GlobalBehavior(),
          translations: Messages(),
          locale: localeService.currentLocale,
          fallbackLocale: localeService.fallbackLocale,
          title: 'OASX',
          initialRoute: Routes.initial,
          getPages: Routes.routes,
          theme: lightTheme,
          // AMOLED 开关开启时用纯黑深色主题
          darkTheme:
              Get.find<ThemeService>().isAmoled ? amoledDarkTheme : darkTheme,
          themeMode: Get.find<ThemeService>().themeMode,
        );
      },
    );
  }
}

class GlobalBehavior extends MaterialScrollBehavior {
  @override
  Set<PointerDeviceKind> get dragDevices => {
    PointerDeviceKind.touch,
    PointerDeviceKind.mouse,
  };
}

Future<void> initService() async {
  await ProfileService.initialize();

  Get.put(SettingsController(), permanent: true);
  if (PlatformUtils.isDesktop) {
    Get.put(SystemTrayService(), permanent: true);
  }
  Get.lazyPut<WebSocketService>(() => WebSocketService(), fenix: true);
  final windowService = Get.put(WindowService());

  await Future.wait([
    initLogger(),
    Get.putAsync(() async => LocaleService()),
    Get.putAsync(() async => ThemeService()),
    Get.putAsync(() async => AutoStartService(), permanent: true),
    Get.putAsync(() async => AppUpdateService(), permanent: true),
    windowService.ready,
    // 亚克力要在窗口就绪后开（flutter_acrylic 与 window_manager 共用底层句柄）
    AcrylicService.initialize(),
    Get.putAsync(() async => ScriptService(), permanent: true),
  ]);
}
