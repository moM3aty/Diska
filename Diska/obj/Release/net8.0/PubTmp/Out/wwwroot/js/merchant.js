// وظائف لوحة تحكم التاجر (تحديث UI)

function previewImage(input) {
    if (input.files && input.files[0]) {
        var reader = new FileReader();
        reader.onload = function (e) {
            const preview = document.getElementById('previewImg');
            if (preview) {
                preview.src = e.target.result;
                preview.style.display = 'block';
            }
            // If banner preview exists
            const bannerPrev = document.getElementById('bannerPreview');
            if (bannerPrev) {
                bannerPrev.src = e.target.result;
                bannerPrev.style.display = 'block';
            }
        };
        reader.readAsDataURL(input.files[0]);
    }
}

function addTierRow() {
    const tbody = document.getElementById('tiersContainer');
    if (!tbody) return;

    const index = tbody.querySelectorAll('tr').length;
    const row = `
        <tr>
            <td><input type="number" name="PriceTiers[${index}].MinQuantity" class="form-control small-input" placeholder="مثال: 10"></td>
            <td><input type="number" name="PriceTiers[${index}].MaxQuantity" class="form-control small-input" placeholder="مثال: 50"></td>
            <td><input type="number" name="PriceTiers[${index}].UnitPrice" class="form-control small-input" placeholder="سعر مخفض"></td>
            <td><button type="button" class="btn-delete" onclick="this.closest('tr').remove()"><i class="fas fa-trash"></i></button></td>
        </tr>
    `;
    tbody.insertAdjacentHTML('beforeend', row);
}