// وظائف لوحة تحكم الأدمن

document.addEventListener('DOMContentLoaded', () => {

    // Toggle Sidebar on Mobile
    const sidebar = document.querySelector('.sidebar');
    const toggleBtn = document.querySelector('.mobile-toggle'); // Ensure this button exists in Admin Layout
    const mainContent = document.querySelector('.main-content');

    if (toggleBtn && sidebar) {
        toggleBtn.addEventListener('click', () => {
            sidebar.classList.toggle('active');
            if (window.innerWidth < 768) {
                // Handle overlay or push logic for mobile
                if (sidebar.classList.contains('active')) {
                    sidebar.style.transform = 'translateX(0)';
                } else {
                    sidebar.style.transform = 'translateX(100%)'; // RTL support
                }
            }
        });
    }

    // Confirm Delete Actions (Global)
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

// دالة مساعدة لطباعة التقارير
function printReport() {
    const originalContents = document.body.innerHTML;
    const printContents = document.querySelector('.card').innerHTML; // Print specific card

    document.body.innerHTML = printContents;
    window.print();
    document.body.innerHTML = originalContents;
    window.location.reload(); // Restore events
}