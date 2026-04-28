const loginForm = document.getElementById("login-form");
const loginButton = document.getElementById("login-button");
const loginMessage = document.getElementById("login-message");

async function preloadWasmAssets() {
    try {
        const script = document.createElement("script");
        script.src = "/_framework/blazor.webassembly.js";
        script.setAttribute("autostart", "false");
        document.head.appendChild(script);

        const resources = ["/_framework/blazor.webassembly.js", "/_framework/dotnet.js"];

        try {
            const response = await fetch("/api/runtime/preload-assets", { cache: "no-store" });
            if (response.ok) {
                const manifestAssets = await response.json();
                if (Array.isArray(manifestAssets)) {
                    manifestAssets.forEach((resource) => {
                        if (typeof resource === "string" && !resources.includes(resource)) {
                            resources.push(resource);
                        }
                    });
                }
            }
        } catch {
        }

        resources.forEach((resource) => {
            fetch(resource, { cache: "force-cache" }).catch(() => null);
        });
    } catch {
    }
}

async function tryAutoRedirect() {
    const token = window.localStorage.getItem("mdv_token");
    if (!token) {
        return;
    }

    try {
        const response = await fetch("/api/auth/me", {
            headers: {
                Authorization: `Bearer ${token}`
            }
        });

        if (response.ok) {
            window.location.replace("/");
            return;
        }
    } catch {
    }

    window.localStorage.removeItem("mdv_token");
    window.localStorage.removeItem("mdv_user");
}

function setMessage(message) {
    loginMessage.textContent = message;
}

loginForm?.addEventListener("submit", async (event) => {
    event.preventDefault();

    const userName = document.getElementById("username")?.value?.trim() || "";
    const password = document.getElementById("password")?.value || "";

    loginButton.disabled = true;
    setMessage("正在验证身份...");

    try {
        const response = await fetch("/api/auth/login", {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify({ userName, password })
        });

        if (!response.ok) {
            setMessage("用户名或密码错误，请重新输入。");
            return;
        }

        const payload = await response.json();
        window.localStorage.setItem("mdv_token", payload.token);
        window.localStorage.setItem("mdv_user", payload.userName);
        setMessage("登录成功，正在进入工作台...");
        window.location.replace("/");
    } catch {
        setMessage("登录请求失败，请确认服务已启动后再试。");
    } finally {
        loginButton.disabled = false;
    }
});

preloadWasmAssets();
tryAutoRedirect();
