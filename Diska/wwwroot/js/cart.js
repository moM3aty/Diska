// DISKA B2B Cart Logic - Updated for API Integration

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

    let name = card.querySelector('h4, h1')?.innerText || "منتج";
    let priceText = card.querySelector('.current-price')?.innerText || "0";
    let price = parseFloat(priceText.replace(/[^0-9.]/g, ''));
    let image = card.querySelector('img')?.src || "/images/default.png";

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
        // Disable checkout button if cart is empty
        const checkoutBtn = document.querySelector('.cart-summary .btn.primary');
        if (checkoutBtn) checkoutBtn.classList.add('disabled-btn');
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
    if (totalEl) totalEl.innerText = (subtotal * 1.14).toFixed(2) + " ج.م"; // Example tax logic
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
    renderCheckoutPreview();
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

    // Shipping is handled by updateShippingCost now
    updateTotalWithShipping(subtotal);
}

// دالة محدثة لجلب سعر الشحن من السيرفر
async function updateShippingCost() {
    const govSelect = document.getElementById('govSelect');
    const citySelect = document.getElementById('citySelect');
    const shippingEl = document.getElementById('shippingDisplay');

    if (!govSelect || !shippingEl) return;

    const gov = govSelect.value;
    const city = citySelect ? citySelect.value : "";

    if (!gov) {
        shippingEl.innerText = "0.00";
        updateTotalWithShipping();
        return;
    }

    try {
        // الاتصال بالـ API الجديد
        const response = await fetch(`/Cart/GetShippingCost?gov=${encodeURIComponent(gov)}&city=${encodeURIComponent(city)}`);
        if (response.ok) {
            const data = await response.json();
            shippingEl.innerText = data.cost.toFixed(2);
            updateTotalWithShipping();
        }
    } catch (e) {
        console.error("Failed to calculate shipping", e);
    }
}

function updateTotalWithShipping(subtotalOverride) {
    const subTotalEl = document.getElementById('subTotalDisplay');
    const shippingEl = document.getElementById('shippingDisplay');
    const grandTotalEl = document.getElementById('grandTotalDisplay');

    if (!subTotalEl || !shippingEl || !grandTotalEl) return;

    const subtotal = subtotalOverride !== undefined ? subtotalOverride : parseFloat(subTotalEl.innerText);
    const shipping = parseFloat(shippingEl.innerText) || 0;

    grandTotalEl.innerText = (subtotal + shipping).toFixed(2) + " ج.م";
}

async function submitOrder() {
    const form = document.getElementById('checkoutForm');
    if (!form.checkValidity()) { form.reportValidity(); return; }

    const cart = getCart();
    if (cart.length === 0) { alert("السلة فارغة!"); return; }

    const shippingText = document.getElementById('shippingDisplay').innerText;

    const orderData = {
        shopName: document.getElementById('shopName').value,
        phone: document.getElementById('phone').value,
        governorate: document.getElementById('govSelect').value,
        city: document.getElementById('city').value,
        address: document.getElementById('address').value,
        deliverySlot: document.getElementById('deliverySlot')?.value || "Anytime",
        notes: document.getElementById('orderNotes')?.value || "",
        paymentMethod: document.querySelector('input[name="payment"]:checked').value,
        shippingCost: parseFloat(shippingText) || 0,
        items: cart.map(i => ({ id: i.id, qty: i.qty, price: i.price }))
    };

    const btn = document.querySelector('button[onclick="submitOrder()"]');

    try {
        btn.disabled = true;
        btn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> جاري المعالجة...';

        const response = await fetch('/Cart/PlaceOrder', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(orderData)
        });

        const result = await response.json();

        if (result.success) {
            localStorage.removeItem(CART_KEY);

            // التوجيه بناءً على نوع الدفع
            if (result.redirectUrl) {
                // دفع أونلاين
                window.location.href = result.redirectUrl;
            } else {
                // كاش أو محفظة
                window.location.href = `/Cart/OrderSuccess?id=${result.orderId}`;
            }
        } else {
            alert("خطأ: " + result.message);
            btn.disabled = false;
            btn.innerHTML = 'تأكيد الطلب';
        }
    } catch (e) {
        console.error(e);
        alert("فشل الاتصال بالخادم");
        btn.disabled = false;
        btn.innerHTML = 'تأكيد الطلب';
    }
}

// Init
document.addEventListener('DOMContentLoaded', updateCartBadge);