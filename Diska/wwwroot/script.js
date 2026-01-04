document.addEventListener('DOMContentLoaded', () => {

    // --- 1. إدارة القوائم الجانبية ---
    const menuBtn = document.querySelector('.mobile-menu-btn');
    const sidebar = document.querySelector('.mobile-sidebar');
    const overlay = document.querySelector('.menu-overlay');
    const closeBtn = document.querySelector('.close-menu');

    if (menuBtn) {
        menuBtn.addEventListener('click', () => {
            sidebar.classList.add('active');
            if (overlay) overlay.classList.add('active');
        });
    }

    const closeMenu = () => {
        if (sidebar) sidebar.classList.remove('active');
        if (overlay) overlay.classList.remove('active');
    };

    if (closeBtn) closeBtn.addEventListener('click', closeMenu);
    if (overlay) overlay.addEventListener('click', closeMenu);

    // --- 2. إدارة السلة (LocalStorage) ---
    let cart = JSON.parse(localStorage.getItem('DISKA_CART')) || [];
    let wishlist = JSON.parse(localStorage.getItem('DISKA_WISHLIST')) || [];

    // تحديث العدادات
    function updateBadges() {
        document.querySelectorAll('.badge').forEach(b => b.innerText = cart.length);
    }
    updateBadges();

    // إضافة للسلة
    window.addToCart = function (btn) {
        const card = btn.closest('.product-card');
        const id = card.dataset.id;
        const name = card.querySelector('h4') ? card.querySelector('h4').innerText : card.querySelector('h1').innerText;
        const priceText = card.querySelector('.current-price').innerText;
        const price = parseFloat(priceText.replace(/[^0-9.]/g, ''));
        const img = card.querySelector('img').src;
        const qtyInput = card.querySelector('.manual-qty');
        const qty = qtyInput ? parseInt(qtyInput.value) : 1;

        const existingItem = cart.find(i => i.id === id);
        if (existingItem) {
            existingItem.qty += qty;
        } else {
            cart.push({ id, title: name, price, image: img, qty });
        }

        localStorage.setItem('DISKA_CART', JSON.stringify(cart));
        updateBadges();
        showToast('تمت الإضافة للسلة بنجاح', 'success');

        // تأثير بصري للزر
        const originalText = btn.innerHTML;
        btn.innerHTML = '<i class="fas fa-check"></i> تم';
        btn.style.background = '#22c55e';
        setTimeout(() => {
            btn.innerHTML = originalText;
            btn.style.background = '';
        }, 1500);
    };

    // ربط أزرار الإضافة (Event Delegation)
    document.body.addEventListener('click', function (e) {
        if (e.target.closest('.add-btn')) {
            addToCart(e.target.closest('.add-btn'));
        }

        // أزرار الزيادة والنقصان في الكروت
        if (e.target.classList.contains('card-plus')) {
            const input = e.target.previousElementSibling;
            input.value = parseInt(input.value) + 1;
        }
        if (e.target.classList.contains('card-minus')) {
            const input = e.target.nextElementSibling;
            if (parseInt(input.value) > 1) input.value = parseInt(input.value) - 1;
        }

        // زر المفضلة
        if (e.target.closest('.fav-btn')) {
            const btn = e.target.closest('.fav-btn');
            const card = btn.closest('.product-card');
            const id = card.dataset.id;

            btn.classList.toggle('active');

            if (btn.classList.contains('active')) {
                // إضافة
                const name = card.querySelector('h4') ? card.querySelector('h4').innerText : card.querySelector('h1').innerText;
                const priceText = card.querySelector('.current-price').innerText;
                const img = card.querySelector('img').src;
                if (!wishlist.some(i => i.id === id)) {
                    wishlist.push({ id, name, price: priceText, image: img });
                    showToast('تمت الإضافة للمفضلة');
                }
            } else {
                // إزالة
                wishlist = wishlist.filter(i => i.id !== id);
                showToast('تم الحذف من المفضلة', 'warning');
            }
            localStorage.setItem('DISKA_WISHLIST', JSON.stringify(wishlist));
        }
    });

    // --- 3. عرض السلة في صفحة السلة ---
    const cartItemsContainer = document.querySelector('.cart-items');
    if (cartItemsContainer) {
        renderCartItems();
    }

    function renderCartItems() {
        if (!cartItemsContainer) return;
        cartItemsContainer.innerHTML = '';
        let total = 0;

        if (cart.length === 0) {
            cartItemsContainer.innerHTML = '<p style="text-align:center; padding:20px;">السلة فارغة</p>';
        } else {
            cart.forEach((item, index) => {
                total += item.price * item.qty;
                cartItemsContainer.innerHTML += `
                    <div class="cart-item">
                        <img src="${item.image}">
                        <div class="item-details">
                            <h4>${item.title}</h4>
                            <span class="price">${item.price} ج.م</span>
                        </div>
                        <div class="item-actions">
                            <div class="qty-control small">
                                <button class="cart-minus" onclick="updateCartQty(${index}, -1)">-</button>
                                <input type="number" value="${item.qty}" readonly>
                                <button class="cart-plus" onclick="updateCartQty(${index}, 1)">+</button>
                            </div>
                            <span class="total-item-price">${(item.price * item.qty).toFixed(2)} ج.م</span>
                            <button class="cart-remove" onclick="removeFromCart(${index})"><i class="fas fa-trash"></i></button>
                        </div>
                    </div>
                `;
            });
        }

        // تحديث الإجماليات
        const subTotalElem = document.getElementById('cartSubTotal');
        const totalElem = document.getElementById('cartTotal');
        if (subTotalElem) subTotalElem.innerText = total.toFixed(2) + ' ج.م';
        // إضافة ضريبة وهمية 14%
        if (totalElem) totalElem.innerText = (total * 1.14).toFixed(2) + ' ج.م';
    }

    window.updateCartQty = function (index, change) {
        if (cart[index].qty + change > 0) {
            cart[index].qty += change;
            localStorage.setItem('DISKA_CART', JSON.stringify(cart));
            renderCartItems();
            updateBadges();
        }
    };

    window.removeFromCart = function (index) {
        cart.splice(index, 1);
        localStorage.setItem('DISKA_CART', JSON.stringify(cart));
        renderCartItems();
        updateBadges();
    };

    // --- 4. نظام التنبيهات (Toast) ---
    function showToast(msg, type = 'success') {
        const container = document.querySelector('.toast-container') || createToastContainer();
        const toast = document.createElement('div');
        toast.className = `toast ${type}`;
        toast.innerHTML = `<span>${msg}</span>`;
        container.appendChild(toast);
        setTimeout(() => {
            toast.style.opacity = '0';
            setTimeout(() => toast.remove(), 300);
        }, 3000);
    }

    function createToastContainer() {
        const div = document.createElement('div');
        div.className = 'toast-container';
        document.body.appendChild(div);
        return div;
    }
});