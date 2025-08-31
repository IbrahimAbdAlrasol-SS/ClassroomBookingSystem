# نشر نظام حجز القاعات على Fly.io

## المتطلبات المسبقة

1. تثبيت Fly CLI:
   ```bash
   # Windows (PowerShell)
   iwr https://fly.io/install.ps1 -useb | iex
   
   # أو باستخدام Chocolatey
   choco install flyctl
   ```

2. إنشاء حساب على Fly.io وتسجيل الدخول:
   ```bash
   flyctl auth signup
   # أو
   flyctl auth login
   ```

## خطوات النشر

### 1. إعداد التطبيق

```bash
# الانتقال إلى مجلد المشروع
cd ClassroomBookingSystem

# إنشاء التطبيق على Fly.io
flyctl launch
```

### 2. إعداد قاعدة البيانات

```bash
# إنشاء قاعدة بيانات PostgreSQL
flyctl postgres create --name classroom-booking-db

# ربط قاعدة البيانات بالتطبيق
flyctl postgres attach --app classroom-booking-api classroom-booking-db
```

### 3. إعداد متغيرات البيئة

```bash
# إعداد مفتاح JWT (استبدل بمفتاح قوي)
flyctl secrets set JWT_SECRET_KEY="YourSuperSecretJWTKeyHere_MustBe32CharsOrMore!"

# سيتم إعداد DATABASE_URL تلقائياً عند ربط قاعدة البيانات
```

### 4. النشر

```bash
# نشر التطبيق
flyctl deploy

# مراقبة السجلات
flyctl logs
```

### 5. فتح التطبيق

```bash
# فتح التطبيق في المتصفح
flyctl open
```

## إعدادات مهمة

### تحديث CORS للإنتاج

في ملف `Program.cs`، قم بتحديث السطر التالي:
```csharp
policy.WithOrigins("https://your-frontend-domain.com")
```

استبدل `your-frontend-domain.com` بالنطاق الفعلي لتطبيق Flutter الخاص بك.

### مراقبة التطبيق

```bash
# عرض حالة التطبيق
flyctl status

# عرض السجلات المباشرة
flyctl logs -f

# الاتصال بقاعدة البيانات
flyctl postgres connect -a classroom-booking-db
```

### تحديث التطبيق

```bash
# بعد إجراء تغييرات على الكود
flyctl deploy
```

## استكشاف الأخطاء

### مشاكل شائعة:

1. **خطأ في الاتصال بقاعدة البيانات:**
   ```bash
   flyctl secrets list
   # تأكد من وجود DATABASE_URL
   ```

2. **مشاكل في JWT:**
   ```bash
   flyctl secrets set JWT_SECRET_KEY="NewSecretKey"
   ```

3. **مشاكل في CORS:**
   - تأكد من تحديث النطاق في `Program.cs`
   - تحقق من إعدادات CORS في التطبيق

### عرض السجلات التفصيلية:

```bash
flyctl logs --app classroom-booking-api
```

## ملاحظات مهمة

- سيتم تشغيل Migration تلقائياً في الإنتاج
- Swagger متاح فقط في بيئة التطوير
- تأكد من إعداد CORS بشكل صحيح للأمان
- استخدم مفاتيح JWT قوية في الإنتاج

## الروابط المفيدة

- [Fly.io Documentation](https://fly.io/docs/)
- [.NET on Fly.io](https://fly.io/docs/languages-and-frameworks/dotnet/)
- [PostgreSQL on Fly.io](https://fly.io/docs/postgres/)