MY CITY v2.167 — УДАЛЁННАЯ ANDROID-СБОРКА
=========================================

Этот комплект предназначен для копирования в локальный Git-репозиторий
D:\MyCity_WORK на ветке android-build-v2.167.

Содержимое:
- .github/workflows/android-build.yml
  GitHub Actions workflow для запуска удалённой Android-сборки.
- Assets/Editor/MyCityAndroidBuild.cs
  Editor build method: автоматически создаёт минимальную стартовую сцену,
  включает её в Build Settings и собирает APK.

ВАЖНО:
1. Не копировать поверх ветки main и не выполнять merge в main.
2. Убедиться, что GitHub Desktop показывает ветку android-build-v2.167.
3. Скопировать оба файла/каталога в корень D:\MyCity_WORK, сохраняя структуру.
4. В GitHub Desktop проверить Changes, затем Commit to
   android-build-v2.167 и Push origin.
5. Перед запуском workflow требуется Unity Personal license secret
   с именем UNITY_LICENSE в Settings -> Secrets and variables -> Actions.
   Не отправляйте файл лицензии в чат и не добавляйте его в репозиторий.
6. После настройки лицензии открыть GitHub -> Actions -> My City Android APK
   -> Run workflow.

ПАРАМЕТРЫ СБОРКИ:
- Unity: 6000.3.29f1
- Target: Android
- APK: build/Android/MyCity.apk
- Application ID: com.zhyrinak1992.mycity
- Version: 2.167

ОГРАНИЧЕНИЯ И ПРОВЕРКИ:
- Этот комплект ещё не отправлен в GitHub и не запускался в Unity.
- Для удалённой сборки GameCI требуется действующая Unity license,
  доступная runner через секрет UNITY_LICENSE.
- В этом блоке не проверялась компиляция C# или фактический APK.
- Проект должен быть совместим с используемой версией Unity и Android-модулем
  на стороне runner. Ошибки Unity Console будут видны в логе GitHub Actions.
