// بيانات المحافظات والمناطق في مصر
const egyptData = {
    "القاهرة": ["مدينة نصر", "مصر الجديدة", "التجمع الخامس", "المعادي", "وسط البلد", "شبرا", "عين شمس", "المرج", "حلوان", "المقطم", "الرحاب", "مدينتي", "الشروق", "حدائق القبة"],
    "الجيزة": ["الدقي", "المهندسين", "الهرم", "فيصل", "6 أكتوبر", "الشيخ زايد", "إمبابة", "الوراق", "المنيب", "العجوزة", "أبو النمرس"],
    "الإسكندرية": ["سموحة", "ميامي", "المنتزه", "محطة الرمل", "العجمي", "سيدي جابر", "العصافرة", "لوران", "كامب شيزار", "برج العرب"],
    "القليوبية": ["بنها", "شبرا الخيمة", "قليوب", "طوخ", "القناطر الخيرية", "الخانكة", "العبور", "كفر شكر"],
    "الدقهلية": ["المنصورة", "ميت غمر", "السنبلاوين", "طلخا", "بلقاس", "دكرنس", "شربين", "أجا"],
    "الشرقية": ["الزقازيق", "العشر من رمضان", "منيا القمح", "بلبيس", "فاقوس", "أبو حماد", "ديرب نجم", "كفر صقر"],
    "الغربية": ["طنطا", "المحلة الكبرى", "كفر الزيات", "زفتى", "السنطة", "بسيون", "قطور"],
    "المنوفية": ["شبين الكوم", "منوف", "أشمون", "قويسنا", "بركة السبع", "تلا", "الباجور", "الشهداء"],
    "البحيرة": ["دمنهور", "كفر الدوار", "إيتاي البارود", "رشيد", "كوم حمادة", "أبو حمص", "حوش عيسى", "الدلنجات"],
    "الإسماعيلية": ["الإسماعيلية", "فايد", "القنطرة شرق", "القنطرة غرب", "التل الكبير", "أبو صوير"],
    "السويس": ["السويس", "الأربعين", "الجناين", "عتاقة", "العين السخنة"],
    "بورسعيد": ["بورسعيد", "بورفؤاد", "حي الشرق", "حي الزهور", "حي العرب"],
    "دمياط": ["دمياط", "رأس البر", "فارسكور", "الزرقا", "دمياط الجديدة", "كفر سعد"],
    "كفر الشيخ": ["كفر الشيخ", "دسوق", "فوه", "مطوبس", "بلطيم", "سيدي سالم", "الحامول"],
    "أسوان": ["أسوان", "كوم أمبو", "إدفو", "دراو", "نصر النوبة", "أبو سمبل"]
};

document.addEventListener('DOMContentLoaded', () => {
    const govSelect = document.getElementById('govSelect');
    const citySelect = document.getElementById('citySelect');
    const shippingDisplay = document.getElementById('shippingCost');
    const totalDisplay = document.getElementById('finalTotal');
    const subTotalDisplay = document.getElementById('subTotal');
    const itemsList = document.getElementById('checkoutItemsList');

    // 1. ملء القائمة المنسدلة للمحافظات
    if (govSelect) {
        for (let gov in egyptData) {
            let option = document.createElement('option');
            option.value = gov;
            option.innerText = gov;
            govSelect.appendChild(option);
        }
    }

    // 2. دالة تحديث المناطق والشحن
    window.updateRegions = function () {
        const selectedGov = govSelect.value;
        const cities = egyptData[selectedGov];

        // تفريغ المناطق القديمة
        citySelect.innerHTML = '<option value="" disabled selected>اختر المنطقة</option>';

        if (cities) {
            cities.forEach(city => {
                let option = document.createElement('option');
                option.value = city;
                option.innerText = city;
                citySelect.appendChild(option);
            });
            citySelect.disabled = false;
        }

        // حساب تكلفة الشحن (تقريبي)
        let shippingFees = 0;
        if (selectedGov === "القاهرة" || selectedGov === "الجيزة") {
            shippingFees = 50;
        } else if (selectedGov === "الإسكندرية") {
            shippingFees = 75;
        } else {
            shippingFees = 100;
        }

        // تحديث الواجهة
        if (shippingDisplay) shippingDisplay.innerText = shippingFees + " ج.م";

        // إعادة حساب الإجمالي
        const cart = JSON.parse(localStorage.getItem('DISKA_CART')) || [];
        const subTotal = cart.reduce((sum, item) => sum + (item.price * item.qty), 0);

        // (SubTotal + Tax 14% + Shipping)
        const finalTotal = (subTotal * 1.14) + shippingFees;

        if (totalDisplay) totalDisplay.innerText = finalTotal.toFixed(2) + " ج.م";
    };

    // 3. عرض منتجات السلة في صفحة الدفع
    if (itemsList) {
        const cart = JSON.parse(localStorage.getItem('DISKA_CART')) || [];
        itemsList.innerHTML = '';
        let subTotal = 0;

        if (cart.length === 0) {
            itemsList.innerHTML = '<p style="text-align:center">لا توجد عناصر</p>';
        } else {
            cart.forEach(item => {
                const itemTotal = item.price * item.qty;
                subTotal += itemTotal;
                itemsList.innerHTML += `
                    <div class="c-item">
                        <img src="${item.image}" style="width:50px; height:50px; object-fit:contain;">
                        <div>
                            <h4>${item.title}</h4>
                            <small>العدد: ${item.qty}</small>
                        </div>
                        <span>${itemTotal.toFixed(2)} ج.م</span>
                    </div>
                `;
            });
        }

        if (subTotalDisplay) subTotalDisplay.innerText = subTotal.toFixed(2) + " ج.م";
        if (totalDisplay) totalDisplay.innerText = (subTotal * 1.14).toFixed(2) + " ج.م"; // مبدئياً بدون شحن
    }
});