document.addEventListener('DOMContentLoaded', () => {
    checkAndLoadSurvey();
});

async function checkAndLoadSurvey() {
    // Check if user has already seen/closed survey recently (localStorage)
    const surveyHidden = sessionStorage.getItem('diska_survey_hidden');
    if (surveyHidden) return;

    try {
        const response = await fetch('/UserSurvey/GetActiveSurvey');
        if (response.ok) {
            const html = await response.text();
            if (html.trim().length > 0) {
                document.body.insertAdjacentHTML('beforeend', html);
                setTimeout(() => {
                    const popup = document.getElementById('surveyPopup');
                    if (popup) popup.style.display = 'flex';
                }, 2000); // Show after 2 seconds
            }
        }
    } catch (e) {
        console.log("No active survey");
    }
}

function closeSurvey() {
    const popup = document.getElementById('surveyPopup');
    if (popup) {
        popup.style.display = 'none';
        sessionStorage.setItem('diska_survey_hidden', 'true');
    }
}

async function submitSurvey(e) {
    e.preventDefault();
    const form = e.target;
    const btn = form.querySelector('button[type="submit"]');
    const formData = new FormData(form);

    try {
        btn.disabled = true;
        btn.innerHTML = '<i class="fas fa-spinner fa-spin"></i>';

        const response = await fetch('/UserSurvey/SubmitResponse', {
            method: 'POST',
            body: formData
        });

        const result = await response.json();

        if (result.success) {
            form.innerHTML = `
                <div style="text-align:center; padding:30px;">
                    <i class="fas fa-check-circle fa-3x" style="color:#10b981; margin-bottom:15px;"></i>
                    <h3 style="color:#10b981;">شكراً لك!</h3>
                    <p>تم استلام إجاباتك بنجاح.</p>
                </div>
            `;
            setTimeout(closeSurvey, 3000);
        } else {
            alert("حدث خطأ، حاول مرة أخرى.");
            btn.disabled = false;
            btn.innerText = "إرسال";
        }
    } catch (error) {
        console.error(error);
        alert("فشل الاتصال بالخادم");
        btn.disabled = false;
        btn.innerText = "إرسال";
    }
}

function updateStars(input) {
    const container = input.closest('.star-rating');
    const stars = container.querySelectorAll('i');
    const val = parseInt(input.value);

    stars.forEach((star, index) => {
        if (index < val) {
            star.classList.remove('far');
            star.classList.add('fas');
        } else {
            star.classList.remove('fas');
            star.classList.add('far');
        }
    });
}