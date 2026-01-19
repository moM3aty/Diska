
const CART_KEY = 'DISKA_CART';

// 1. Add Item to Cart
async function addItemToCart(id, qty, btnElement) {
    if (!qty || qty < 1) qty = 1;

    // Show Loading
    let originalText = "";
    if (btnElement) {
        originalText = btnElement.innerHTML;
        btnElement.disabled = true;
        btnElement.innerHTML = '<i class="fas fa-spinner fa-spin"></i>';
    }

    try {
        // Validate with Server first
        const response = await fetch('/Cart/AddItem', {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
            body: `productId=${id}&quantity=${qty}`
        });

        const result = await response.json();

        if (result.success) {
            // Add to LocalStorage
            let cart = JSON.parse(localStorage.getItem(CART_KEY)) || [];
            let existingItem = cart.find(i => i.id == id);

            if (existingItem) {
                existingItem.qty = parseInt(existingItem.qty) + parseInt(qty);
            } else {
                cart.push({
                    id: id,
                    qty: parseInt(qty),
                    name: result.product.name,
                    price: result.product.price,
                    image: result.product.image,
                    maxStock: result.product.stock
                });
            }

            localStorage.setItem(CART_KEY, JSON.stringify(cart));
            updateCartBadge();

            // Success Toast
            Swal.fire({
                toast: true,
                position: 'top-end',
                icon: 'success',
                title: result.message,
                showConfirmButton: false,
                timer: 1500
            });
        } else {
            // Error Alert
            Swal.fire({
                icon: 'warning',
                title: 'تنبيه',
                text: result.message,
                confirmButtonColor: '#2563eb'
            });
        }
    } catch (error) {
        console.error(error);
        Swal.fire({ icon: 'error', title: 'خطأ', text: 'فشل الاتصال بالخادم.' });
    } finally {
        // Reset Button
        if (btnElement) {
            btnElement.disabled = false;
            btnElement.innerHTML = originalText;
        }
    }
}

// 2. Update Cart Badge
function updateCartBadge() {
    const cart = JSON.parse(localStorage.getItem(CART_KEY)) || [];
    const badge = document.getElementById('cartBadge');
    const totalEl = document.getElementById('headerCartTotal');

    if (badge) {
        badge.innerText = cart.length;
        badge.style.display = cart.length > 0 ? 'flex' : 'none';
    }

    if (totalEl) {
        let total = cart.reduce((sum, item) => sum + (item.price * item.qty), 0);
        totalEl.innerText = total.toLocaleString() + ' ج.م';
    }
}

// 3. Render Cart Page (Views/Cart/Index.cshtml)
function renderCartPage() {
    const container = document.getElementById('cartItemsList');
    if (!container) return;

    const cart = JSON.parse(localStorage.getItem(CART_KEY)) || [];
    const subtotalEl = document.getElementById('cartSubtotal');
    const totalEl = document.getElementById('cartTotal');

    if (cart.length === 0) {
        container.innerHTML = `
            <div class="text-center py-5">
                <i class="fas fa-shopping-cart fa-3x text-muted mb-3 opacity-25"></i>
                <h4 class="text-muted">سلة المشتريات فارغة</h4>
                <a href="/" class="btn btn-primary mt-3">تصفح المنتجات</a>
            </div>
        `;
        if (subtotalEl) subtotalEl.innerText = "0.00";
        if (totalEl) totalEl.innerText = "0.00";
        return;
    }

    let html = '';
    let total = 0;

    cart.forEach((item, index) => {
        let itemTotal = item.price * item.qty;
        total += itemTotal;

        html += `
            <div class="cart-item d-flex align-items-center gap-3 border-bottom pb-3 mb-3">
                <img src="/${item.image}" onerror="this.src='/images/default.png'" style="width:70px; height:70px; object-fit:cover; border-radius:8px; border:1px solid #eee;">
                
                <div class="flex-grow-1">
                    <h5 class="mb-1" style="font-size:1rem;">
                        <a href="/Product/Details/${item.id}" class="text-decoration-none text-dark">${item.name}</a>
                    </h5>
                    <div class="text-primary fw-bold">${item.price.toLocaleString()} ج.م</div>
                </div>

                <div class="d-flex align-items-center gap-2">
                    <button class="btn btn-sm btn-light border" onclick="changeCartQty(${index}, -1)">-</button>
                    <input type="number" class="form-control form-control-sm text-center" value="${item.qty}" style="width:50px;" readonly>
                    <button class="btn btn-sm btn-light border" onclick="changeCartQty(${index}, 1)">+</button>
                </div>

                <div class="fw-bold ms-3" style="min-width:80px; text-align:end;">
                    ${itemTotal.toLocaleString()}
                </div>

                <button class="btn btn-sm text-danger" onclick="removeCartItem(${index})"><i class="fas fa-trash"></i></button>
            </div>
        `;
    });

    container.innerHTML = html;
    if (subtotalEl) subtotalEl.innerText = total.toLocaleString();
    if (totalEl) totalEl.innerText = total.toLocaleString();

    // Update Header Badge too
    updateCartBadge();
}

// 4. Helper Functions for Cart Page
function changeCartQty(index, change) {
    let cart = JSON.parse(localStorage.getItem(CART_KEY)) || [];
    let item = cart[index];

    let newQty = parseInt(item.qty) + change;

    if (newQty > item.maxStock) {
        Swal.fire({ icon: 'warning', text: `أقصى كمية متاحة هي ${item.maxStock}` });
        return;
    }

    if (newQty < 1) {
        removeCartItem(index);
        return;
    }

    item.qty = newQty;
    localStorage.setItem(CART_KEY, JSON.stringify(cart));
    renderCartPage();
}

function removeCartItem(index) {
    let cart = JSON.parse(localStorage.getItem(CART_KEY)) || [];
    cart.splice(index, 1);
    localStorage.setItem(CART_KEY, JSON.stringify(cart));
    renderCartPage();
}

// 5. Render Checkout Preview (Views/Cart/Checkout.cshtml)
function renderCheckoutPreview() {
    const container = document.getElementById('checkoutItems');
    if (!container) return;

    const cart = JSON.parse(localStorage.getItem(CART_KEY)) || [];
    let subtotal = 0;
    let html = '';

    cart.forEach(item => {
        let itemTotal = item.price * item.qty;
        subtotal += itemTotal;
        html += `
            <div class="d-flex align-items-center justify-content-between mb-2 pb-2 border-bottom small">
                <div class="d-flex align-items-center gap-2">
                    <img src="/${item.image}" style="width:40px; height:40px; object-fit:cover; border-radius:4px;">
                    <div>
                        <div class="fw-bold text-truncate" style="max-width:150px;">${item.name}</div>
                        <div class="text-muted">x${item.qty}</div>
                    </div>
                </div>
                <div class="fw-bold">${itemTotal.toLocaleString()}</div>
            </div>
        `;
    });

    container.innerHTML = html;

    const subDisplay = document.getElementById('subTotalDisplay');
    const grandDisplay = document.getElementById('grandTotalDisplay');
    const shipDisplay = document.getElementById('shippingDisplay');

    if (subDisplay) subDisplay.innerText = subtotal.toLocaleString();

    // Initial Calc
    if (typeof updateShippingCost === 'function') updateShippingCost();
}

// Init on Load
document.addEventListener('DOMContentLoaded', updateCartBadge);