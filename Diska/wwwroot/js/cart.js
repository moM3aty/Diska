// DISKA B2B Cart Logic - LocalStorage & API Sync

const CART_KEY = 'DISKA_CART';

// --- Core Functions ---

function getCart() {
    return JSON.parse(localStorage.getItem(CART_KEY)) || [];
}

function saveCart(cart) {
    localStorage.setItem(CART_KEY, JSON.stringify(cart));
    updateCartBadge();
}

function addToCart(btnElement) {
    const card = btnElement.closest('.product-card') || btnElement.closest('.details-info');
    if (!card) return;

    const id = card.dataset.id;
    const stock = parseInt(card.dataset.stock || 9999);

    // Attempt to get name/price/image from DOM structure
    let name = card.querySelector('h4, h1')?.innerText || "Unknown Product";
    let priceText = card.querySelector('.current-price')?.innerText || "0";
    let price = parseFloat(priceText.replace(/[^0-9.]/g, ''));
    let image = card.querySelector('img')?.src || "/images/default.png";

    // Check for qty input (details page or merchant card)
    let qtyInput = card.querySelector('.manual-qty');
    let qtyToAdd = qtyInput ? parseInt(qtyInput.value) : 1;

    let cart = getCart();
    let existing = cart.find(i => i.id === id);

    if (existing) {
        if (existing.qty + qtyToAdd > stock) {
            alert("عفواً، الكمية المطلوبة تتجاوز المخزون المتاح");
            return;
        }
        existing.qty += qtyToAdd;
    } else {
        if (qtyToAdd > stock) {
            alert("عفواً، الكمية المطلوبة تتجاوز المخزون المتاح");
            return;
        }
        cart.push({ id, name, price, image, qty: qtyToAdd, stock });
    }

    saveCart(cart);

    // Visual Feedback
    const originalText = btnElement.innerHTML;
    btnElement.innerHTML = '<i class="fas fa-check"></i> تم';
    btnElement.classList.add('success-anim');
    setTimeout(() => {
        btnElement.innerHTML = originalText;
        btnElement.classList.remove('success-anim');
    }, 1500);
}

function updateCartBadge() {
    const cart = getCart();
    const badge = document.getElementById('cartBadge');
    if (badge) badge.innerText = cart.length;
}

// --- Cart Page Rendering ---

function renderCartPage() {
    const list = document.getElementById('cartItemsList');
    const subtotalEl = document.getElementById('cartSubtotal');
    const totalEl = document.getElementById('cartTotal');

    if (!list) return;

    const cart = getCart();
    list.innerHTML = '';
    let subtotal = 0;

    if (cart.length === 0) {
        list.innerHTML = `<div style="text-align:center; padding:40px;">السلة فارغة</div>`;
        if (subtotalEl) subtotalEl.innerText = "0.00";
        if (totalEl) totalEl.innerText = "0.00";
        return;
    }

    cart.forEach((item, index) => {
        let itemTotal = item.price * item.qty;
        subtotal += itemTotal;

        list.innerHTML += `
            <div class="cart-item" style="display:flex; gap:15px; border-bottom:1px solid #eee; padding:15px 0; align-items:center;">
                <img src="${item.image}" style="width:70px; height:70px; object-fit:contain; border:1px solid #eee; border-radius:8px;">
                <div style="flex:1;">
                    <h4 style="margin:0 0 5px; font-size:1rem;">${item.name}</h4>
                    <div style="color:#666;">${item.price.toFixed(2)} ج.م</div>
                </div>
                <div class="qty-control small" style="display:flex; align-items:center; border:1px solid #ddd; border-radius:5px;">
                    <button onclick="updateCartItem(${index}, -1)" style="border:none; background:#eee; width:25px;">-</button>
                    <input value="${item.qty}" readonly style="width:35px; text-align:center; border:none;">
                    <button onclick="updateCartItem(${index}, 1)" style="border:none; background:#eee; width:25px;">+</button>
                </div>
                <div style="font-weight:bold; min-width:80px; text-align:right;">${itemTotal.toFixed(2)}</div>
                <button onclick="removeCartItem(${index})" style="color:red; border:none; background:none; cursor:pointer;"><i class="fas fa-trash"></i></button>
            </div>
        `;
    });

    if (subtotalEl) subtotalEl.innerText = subtotal.toFixed(2);
    if (totalEl) totalEl.innerText = subtotal.toFixed(2) + " ج.م";
}

function updateCartItem(index, change) {
    let cart = getCart();
    let item = cart[index];

    let newQty = item.qty + change;

    if (newQty > item.stock) {
        alert("الكمية تجاوزت المخزون المتاح");
        return;
    }

    if (newQty > 0) {
        item.qty = newQty;
    }

    saveCart(cart);
    renderCartPage();
    renderCheckoutPreview(); // If on checkout page
}

function removeCartItem(index) {
    let cart = getCart();
    cart.splice(index, 1);
    saveCart(cart);
    renderCartPage();
    renderCheckoutPreview();
}

// --- Checkout Logic ---

function renderCheckoutPreview() {
    const container = document.getElementById('checkoutItems');
    const subTotalEl = document.getElementById('subTotalDisplay');
    const grandTotalEl = document.getElementById('grandTotalDisplay');
    const shippingEl = document.getElementById('shippingDisplay');

    if (!container) return;

    const cart = getCart();
    container.innerHTML = '';
    let subtotal = 0;

    cart.forEach(item => {
        subtotal += item.price * item.qty;
        container.innerHTML += `
            <div style="display:flex; justify-content:space-between; margin-bottom:10px; font-size:0.9rem;">
                <span>${item.name} <small>x${item.qty}</small></span>
                <span>${(item.price * item.qty).toFixed(2)}</span>
            </div>
        `;
    });

    if (subTotalEl) subTotalEl.innerText = subtotal.toFixed(2);

    let shipping = parseFloat(shippingEl ? shippingEl.innerText : 50);
    if (grandTotalEl) grandTotalEl.innerText = (subtotal + shipping).toFixed(2) + " ج.م";
}

function updateShippingCost() {
    const gov = document.getElementById('govSelect').value;
    const shippingEl = document.getElementById('shippingDisplay');
    let cost = 100;

    if (gov === 'القاهرة' || gov === 'الجيزة') cost = 50;
    else if (gov === 'الإسكندرية') cost = 75;

    if (shippingEl) shippingEl.innerText = cost.toFixed(2);
    renderCheckoutPreview();
}

async function submitOrder() {
    const form = document.getElementById('checkoutForm');
    if (!form.checkValidity()) {
        form.reportValidity();
        return;
    }

    const cart = getCart();
    if (cart.length === 0) {
        alert("السلة فارغة!");
        return;
    }

    const orderData = {
        shopName: document.getElementById('shopName').value,
        phone: document.getElementById('phone').value,
        governorate: document.getElementById('govSelect').value,
        city: document.getElementById('city').value,
        address: document.getElementById('address').value,
        paymentMethod: document.querySelector('input[name="payment"]:checked').value,
        items: cart.map(i => ({ id: i.id, qty: i.qty }))
    };

    try {
        const btn = document.querySelector('button[onclick="submitOrder()"]');
        btn.disabled = true;
        btn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> جاري التنفيذ...';

        const response = await fetch('/Cart/PlaceOrder', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(orderData)
        });

        const result = await response.json();

        if (result.success) {
            localStorage.removeItem(CART_KEY);
            alert("تم إرسال الطلب بنجاح! رقم الطلب: #" + result.orderId);
            window.location.href = '/Order/MyOrders';
        } else {
            alert("خطأ: " + result.message);
            btn.disabled = false;
            btn.innerHTML = 'تأكيد الطلب';
        }
    } catch (e) {
        console.error(e);
        alert("فشل الاتصال بالخادم");
    }
}

// Init
document.addEventListener('DOMContentLoaded', updateCartBadge);