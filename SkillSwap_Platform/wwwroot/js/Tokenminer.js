document.addEventListener("DOMContentLoaded", () => {
    const dot = document.getElementById("mining-dot");
    const balSpan = document.getElementById("token-balance");
    if (!dot || !balSpan) return;

    dot.classList.add("active");

    // poll display every 30s
    setInterval(async () => {
        try {
            console.log("⏳ Fetching mining status…");
            const resp = await fetch('/api/MiningApi/status', {
                headers: { 'Accept': 'application/json' }
            });
            if (!resp.ok) throw new Error(resp.statusText);
            const st = await resp.json();
            console.log("✅ Raw mining status:", st);

            // if balance changed, log & update
            const newBal = parseFloat(st.balance).toFixed(4);
            if (balSpan.textContent !== newBal) {
                console.log(`🔄 Balance changed: ${balSpan.textContent} → ${newBal}`);
                balSpan.textContent = newBal;
            } else {
                console.log("ℹ️ Balance unchanged:", newBal);
            }

            console.log(`— LastEmittedUtc: ${st.lastEmittedUtc}, EmittedToday: ${st.emittedToday}`);
        } catch (e) {
            console.error("❌ Mining status poll failed:", e);
        }
    }, 30_000);
});