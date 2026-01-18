// الوظائف الأساسية للنظام (الإشعارات، اللغة، التنسيق)

document.addEventListener('DOMContentLoaded', () => {
    // تشغيل التحديث التلقائي للإشعارات كل 30 ثانية
    updateNotificationBadge();
    setInterval(updateNotificationBadge, 30000);

    // تفعيل رسائل التنبيه (Toast) إذا وجدت في الصفحة
    const toasts = document.querySelectorAll('.toast');
    toasts.forEach(t => {
        setTimeout(() => {
            t.classList.add('hide');
            setTimeout(() => t.remove(), 500);
        }, 4000);
    });
});

// تحديث عداد الإشعارات في الهيدر
async function updateNotificationBadge() {
    try {
        const badge = document.querySelector('.notif-badge');
        if (!badge) return;

        const response = await fetch('/Notification/GetUnreadCount');
        if (response.ok) {
            const count = await response.json();
            badge.innerText = count > 99 ? '99+' : count;

            if (count > 0) {
                badge.style.display = 'inline-block';
                badge.classList.add('pulse-animation');
            } else {
                badge.style.display = 'none';
                badge.classList.remove('pulse-animation');
            }
        }
    } catch (e) {
        // Silent error (user might not be logged in)
    }
}

// عرض رسالة تنبيه منبثقة (Toast)
function showToast(message, type = 'success') {
    const container = document.querySelector('.toast-container');
    if (!container) return;

    const toast = document.createElement('div');
    toast.className = `toast ${type}`;
    toast.innerHTML = `
        <i class="fas ${type === 'success' ? 'fa-check-circle' : 'fa-exclamation-circle'}"></i>
        <span>${message}</span>
    `;

    container.appendChild(toast);

    // Animation
    setTimeout(() => toast.classList.add('show'), 10);

    // Auto remove
    setTimeout(() => {
        toast.classList.remove('show');
        setTimeout(() => toast.remove(), 300);
    }, 3000);
}

// تغيير اللغة (يتم استدعاؤه من الزر العائم)
async function switchLanguage(culture) {
    try {
        const currentUrl = window.location.pathname + window.location.search;
        await fetch(`/Language/SetLanguage?culture=${culture}&returnUrl=${encodeURIComponent(currentUrl)}`, {
            method: 'POST'
        });
        window.location.reload();
    } catch (e) {
        console.error("Language switch failed", e);
    }
}