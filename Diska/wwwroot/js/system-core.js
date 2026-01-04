
document.addEventListener('DOMContentLoaded', () => {

    const notifBadge = document.querySelector('.notif-badge');

    window.updateNotificationBadge = async function () {
        try {
    
            const response = await fetch('/Notification/GetUnreadCount');
            if (response.ok) {
                const count = await response.json();
                if (notifBadge) {
                    notifBadge.innerText = count;
                    notifBadge.style.display = count > 0 ? 'block' : 'none';

                    if (count > 0) {
                        const bell = notifBadge.parentElement.querySelector('i');
                        if (bell) bell.classList.add('fa-shake');
                    }
                }
            }
        } catch (e) {
            console.log("User not logged in or API error");
        }
    };

    setInterval(updateNotificationBadge, 30000);
    updateNotificationBadge();

    const langBtn = document.getElementById('langSwitcher');
    if (langBtn) {
        langBtn.addEventListener('click', function (e) {
            e.preventDefault();
            const currentDir = document.documentElement.dir;
            const newLang = currentDir === 'rtl' ? 'en' : 'ar';

            fetch(`/Language/SetLanguage?culture=${newLang === 'en' ? 'en-US' : 'ar-EG'}&returnUrl=${window.location.pathname}`, {
                method: 'POST'
            }).then(() => {
                location.reload();
            });
        });
    }


});