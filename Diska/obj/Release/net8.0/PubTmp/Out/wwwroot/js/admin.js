document.addEventListener('DOMContentLoaded', () => {

    initTheme();
    loadNotifications();

    const sidebar = document.querySelector('.sidebar');
    const toggleBtn = document.querySelector('.mobile-toggle');

    if (toggleBtn && sidebar) {
        toggleBtn.addEventListener('click', () => {
            sidebar.classList.toggle('active');
            if (window.innerWidth < 768) {
                if (sidebar.classList.contains('active')) {
                    sidebar.style.transform = 'translateX(0)';
                } else {
                    const dir = document.documentElement.dir || 'rtl';
                    sidebar.style.transform = dir === 'ltr' ? 'translateX(-100%)' : 'translateX(100%)';
                }
            }
        });
    }

    const deleteForms = document.querySelectorAll('form[data-confirm]');
    deleteForms.forEach(form => {
        form.addEventListener('submit', (e) => {
            const msg = form.dataset.confirm || 'هل أنت متأكد من الحذف؟';
            if (!confirm(msg)) {
                e.preventDefault();
            }
        });
    });
});

function printReport() {
    const originalContents = document.body.innerHTML;
    const printContents = document.querySelector('.card').innerHTML;

    document.body.innerHTML = printContents;
    window.print();
    document.body.innerHTML = originalContents;
    window.location.reload();
}

// --- Dark Mode Logic ---
function initTheme() {
    const toggleBtn = document.getElementById('themeToggle');
    if (!toggleBtn) return;

    const html = document.documentElement;
    const currentTheme = getCookie('theme') || 'light';

    html.setAttribute('data-bs-theme', currentTheme);
    updateThemeIcon(currentTheme);

    toggleBtn.addEventListener('click', () => {
        const newTheme = html.getAttribute('data-bs-theme') === 'dark' ? 'light' : 'dark';
        html.setAttribute('data-bs-theme', newTheme);
        document.cookie = `theme=${newTheme}; path=/; max-age=31536000`;
        updateThemeIcon(newTheme);
    });
}

function updateThemeIcon(theme) {
    const toggleBtn = document.getElementById('themeToggle');
    if (toggleBtn) {
        toggleBtn.innerHTML = theme === 'dark'
            ? '<i class="fas fa-sun text-warning"></i>'
            : '<i class="far fa-moon"></i>';
    }
}

function getCookie(name) {
    const value = `; ${document.cookie}`;
    const parts = value.split(`; ${name}=`);
    if (parts.length === 2) return parts.pop().split(';').shift();
}

// --- Notifications Logic ---
function loadNotifications() {
    const list = document.getElementById('notificationList');
    const badge = document.getElementById('notificationCount');
    if (!list) return;

    fetch('/Admin/Dashboard/GetNotifications')
        .then(response => response.json())
        .then(data => {
            list.innerHTML = '';

            if (data.length === 0) {
                list.innerHTML = '<li><span class="dropdown-item text-center small text-muted">لا توجد إشعارات جديدة</span></li>';
                if (badge) badge.style.display = 'none';
            } else {
                if (badge) {
                    badge.innerText = data.length;
                    badge.style.display = 'block';
                }

                list.innerHTML += '<li><h6 class="dropdown-header">الإشعارات</h6></li><li><hr class="dropdown-divider"></li>';

                data.forEach(n => {
                    const item = document.createElement('li');
                    item.innerHTML = `
                        <a class="dropdown-item d-flex align-items-start gap-2 p-2" href="${n.link}" onclick="markAsRead(${n.id})">
                            <div class="bg-primary bg-opacity-10 text-primary rounded-circle p-1 d-flex align-items-center justify-content-center" style="width:30px; height:30px;">
                                <i class="fas fa-bell small"></i>
                            </div>
                            <div>
                                <strong class="d-block small text-wrap">${n.title}</strong>
                                <small class="text-muted d-block text-truncate" style="max-width: 200px;">${n.message}</small>
                                <span class="text-xs text-secondary" style="font-size: 0.7rem;">${n.timeAgo}</span>
                            </div>
                        </a>
                    `;
                    list.appendChild(item);
                });

                list.innerHTML += '<li><hr class="dropdown-divider"></li><li><a href="/Admin/Dashboard/AllNotifications" class="dropdown-item text-center small fw-bold text-primary">عرض الكل</a></li>';
            }
        })
        .catch(err => console.error('Error loading notifications:', err));
}

function markAsRead(id) {
    fetch(`/Admin/Dashboard/MarkNotificationRead?id=${id}`, { method: 'POST' });
}