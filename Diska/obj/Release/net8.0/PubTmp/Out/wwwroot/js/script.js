document.addEventListener('DOMContentLoaded', () => {

    // --- Mobile Menu Toggle ---
    const menuBtn = document.querySelector('.mobile-menu-btn');
    const closeBtn = document.querySelector('.close-menu');
    const sidebar = document.querySelector('.mobile-sidebar');
    const overlay = document.querySelector('.menu-overlay');

    if (menuBtn && sidebar && overlay) {
        menuBtn.addEventListener('click', () => {
            sidebar.classList.add('active');
            overlay.classList.add('active');
        });

        const closeMenu = () => {
            sidebar.classList.remove('active');
            overlay.classList.remove('active');
        };

        if (closeBtn) closeBtn.addEventListener('click', closeMenu);
        overlay.addEventListener('click', closeMenu);
    }

    // --- Language Switcher ---
    const langBtn = document.getElementById('langSwitcher');
    if (langBtn) {
        langBtn.addEventListener('click', () => {
            const currentUrl = window.location.pathname + window.location.search;
            const isAr = document.documentElement.dir === 'rtl';
            const newCulture = isAr ? 'en-US' : 'ar-EG';

            // Call the SetLanguage action
            fetch(`/Language/SetLanguage?culture=${newCulture}&returnUrl=${encodeURIComponent(currentUrl)}`, {
                method: 'POST'
            }).then(() => {
                window.location.reload();
            });
        });
    }

    // --- Scroll Animations (Optional) ---
    const observer = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                entry.target.classList.add('fade-in-up');
            }
        });
    });

    document.querySelectorAll('.product-card').forEach(el => observer.observe(el));
});