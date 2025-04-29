document.addEventListener("DOMContentLoaded", () => {
    const dot = document.getElementById("mining-dot");
    const balSpan = document.getElementById("token-balance");
    if (!dot || !balSpan) return;

    dot.classList.add("active");

    // poll display every 30s
    setInterval(async () => {
        try {
            const resp = await fetch('/api/MiningApi/status', {
                headers: { 'Accept': 'application/json' }
            });
            if (!resp.ok) throw new Error(resp.statusText);
            const st = await resp.json();

            // if balance changed, log & update
            const newBal = parseFloat(st.balance).toFixed(4);
            if (balSpan.textContent !== newBal) {
                balSpan.textContent = newBal;
            }
        } catch (e) {
            // silently ignore errors
        }
    }, 30_000);
});