// هذا الملف يجمع كل الوظائف الحيوية (إشعارات، ترجمة، تحديث)

document.addEventListener('DOMContentLoaded', () => {

    // 1. نظام الإشعارات (Real-time Simulation)
    // في بيئة الإنتاج نستخدم SignalR، هنا سنستخدم Polling بسيط
    const notifBadge = document.querySelector('.notif-badge');
    const notifListContainer = document.getElementById('notifList');

    function checkNotifications() {
        // الاتصال بـ API لجلب الإشعارات الجديدة
        // fetch('/Notification/GetUnread') ...

        // محاكاة: قراءة من LocalStorage للتجربة
        const notifs = JSON.parse(localStorage.getItem('DISKA_NOTIFICATIONS')) || [];
        const unreadCount = notifs.filter(n => !n.read).length;

        if (notifBadge) {
            notifBadge.innerText = unreadCount;
            notifBadge.style.display = unreadCount > 0 ? 'block' : 'none';
        }
    }

    // تشغيل الفحص كل 10 ثواني
    setInterval(checkNotifications, 10000);
    checkNotifications(); // تشغيل فوري

    // 2. زر الترجمة (Language Switcher)
    const langBtn = document.getElementById('langSwitcher');
    if (langBtn) {
        langBtn.addEventListener('click', function (e) {
            e.preventDefault();

            // التبديل بين LTR و RTL
            const currentDir = document.documentElement.dir;
            const newLang = currentDir === 'rtl' ? 'en' : 'ar';

            // إرسال طلب للباك إند لحفظ الكوكيز (لضبط التواريخ والعملات)
            fetch(`/Language/SetLanguage?culture=${newLang === 'en' ? 'en-US' : 'ar-EG'}&returnUrl=${window.location.pathname}`, {
                method: 'POST'
            }).then(() => {
                // في حالة وجود صفحات منفصلة (index-en.html)، نقوم بالتحويل
                // أو نقوم فقط بإعادة التحميل ليقوم الـ View بقراءة الكوكيز وترجمة النصوص
                location.reload();
            });
        });
    }

    // 3. تحديثات عامة للواجهة
    // تفعيل الـ Tooltips (إذا كنت تستخدم Bootstrap)
    // $('[data-toggle="tooltip"]').tooltip();
});