document.addEventListener("DOMContentLoaded", () => {
    const dot = document.getElementById("mining-dot");
    const balSpan = document.getElementById("token-balance");
    if (!dot || !balSpan) return;

    dot.classList.add("active");

    async function refreshBalance() {
        try {
            const resp = await fetch('/api/MiningApi/status', {
                headers: { 'Accept': 'application/json' }
            });
            if (!resp.ok) throw new Error(`HTTP ${resp.status}: ${resp.statusText}`);
            const st = await resp.json();

            const newBal = parseFloat(st.balance ?? st.Balance).toFixed(4);
            if (balSpan.textContent !== newBal) {
                balSpan.textContent = newBal;
            }
            // clear any previous error state
            dot.classList.remove("error");
            dot.removeAttribute("title");

        } catch {
            // indicate an error for the user
            dot.classList.add("error");
            dot.title = "Failed to refresh balance";
            // remove the error indicator after 1s
            setTimeout(() => {
                dot.classList.remove("error");
                dot.removeAttribute("title");
            }, 1000);
        }
    }

    // initial fetch
    refreshBalance();

    // poll every 30 seconds
    setInterval(refreshBalance, 30_000);
});

//document.addEventListener("DOMContentLoaded", () => {
//    const dot = document.getElementById("mining-dot");
//    const balSpan = document.getElementById("token-balance");
//    if (!dot || !balSpan) return;

//    dot.classList.add("active");

//    // poll display every 30s
//    setInterval(async () => {
//        try {
//            const resp = await fetch('/api/MiningApi/status', {
//                headers: { 'Accept': 'application/json' }
//            });
//            if (!resp.ok) throw new Error(resp.statusText);
//            const st = await resp.json();

//            // if balance changed, log & update
//            const newBal = parseFloat(st.balance).toFixed(4);
//            if (balSpan.textContent !== newBal) {
//                balSpan.textContent = newBal;
//            }

//        } catch (e) {
//            console.error("❌ Mining status poll failed:", e);
//        }
//    }, 30_000);
//});
