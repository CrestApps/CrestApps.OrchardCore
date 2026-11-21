// Scoring help on the Surveys grading page: asks the server for a suggested score and comment for one answer and fills
// the grading form. The grader reads the suggestion and decides; the grade is saved only when they save the form.

const antiforgeryToken = () => document.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';

const fill = (questionId, data) => {
    const points = document.getElementById(`points-${questionId}`);
    const comment = document.getElementById(`comment-${questionId}`);
    const assisted = document.getElementById(`ai-${questionId}`);

    if (points && data.points !== null && data.points !== undefined) {
        points.value = String(data.points);
    }

    if (comment && data.comment) {
        comment.value = data.comment;
    }

    if (assisted) {
        assisted.checked = true;
    }
};

const ask = async (button) => {
    const questionId = button.dataset.questionId;
    const status = document.querySelector(`[data-survey-scoring-help-status="${CSS.escape(questionId)}"]`);
    const label = button.innerHTML;

    button.disabled = true;
    button.textContent = button.dataset.busyText;

    try {
        const response = await fetch(button.dataset.surveyScoringHelp, {
            method: 'POST',
            headers: { RequestVerificationToken: antiforgeryToken() },
            credentials: 'same-origin',
        });

        if (!response.ok) {
            throw new Error(String(response.status));
        }

        const data = await response.json();

        if (data.error) {
            if (status) {
                status.textContent = data.error;
            }

            return;
        }

        fill(questionId, data);

        if (status) {
            status.textContent = '';
        }
    } catch {
        if (status) {
            status.textContent = '!';
        }
    } finally {
        button.disabled = false;
        button.innerHTML = label;
    }
};

document.addEventListener('click', (event) => {
    const button = event.target instanceof Element ? event.target.closest('[data-survey-scoring-help]') : null;

    if (button) {
        event.preventDefault();
        ask(button);
    }
});
