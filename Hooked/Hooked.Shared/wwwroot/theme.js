window.hookedTheme = (function () {
    const key = "hooked-theme";

    function resolveTheme(value) {
        if (value === "dark" || value === "light") {
            return value;
        }

        if (window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)").matches) {
            return "dark";
        }

        return "light";
    }

    function applyTheme(value) {
        const theme = resolveTheme(value);
        document.documentElement.setAttribute("data-theme", theme);
        localStorage.setItem(key, theme);
        return theme;
    }

    function getTheme() {
        return applyTheme(localStorage.getItem(key));
    }

    function setTheme(theme) {
        return applyTheme(theme);
    }

    function toggleTheme() {
        const current = getTheme();
        return applyTheme(current === "dark" ? "light" : "dark");
    }

    return {
        getTheme,
        setTheme,
        toggleTheme
    };
})();
