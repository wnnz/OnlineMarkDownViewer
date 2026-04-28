window.mdViewerAuth = {
    getToken() {
        return window.localStorage.getItem("mdv_token");
    },
    clearToken() {
        window.localStorage.removeItem("mdv_token");
        window.localStorage.removeItem("mdv_user");
    },
    redirectToLogin() {
        window.location.replace("/login.html");
    }
};

window.mdViewerUi = (() => {
    const markdownState = new WeakMap();

    function slugify(text) {
        return text
            .toLowerCase()
            .trim()
            .replace(/[^\w\u4e00-\u9fa5-]+/g, "-")
            .replace(/^-+|-+$/g, "") || `heading-${Date.now()}`;
    }

    function decorateCodeBlocks(contentElement) {
        const blocks = contentElement.querySelectorAll("pre > code");
        blocks.forEach((codeBlock) => {
            const pre = codeBlock.parentElement;
            if (!pre || pre.dataset.decorated === "true") {
                if (window.hljs) {
                    window.hljs.highlightElement(codeBlock);
                }
                return;
            }

            pre.dataset.decorated = "true";
            if (window.hljs) {
                window.hljs.highlightElement(codeBlock);
            }

            const wrapper = document.createElement("div");
            wrapper.className = "code-block-shell";

            const toolbar = document.createElement("div");
            toolbar.className = "code-toolbar";

            const language = Array.from(codeBlock.classList)
                .find((item) => item.startsWith("language-"))
                ?.replace("language-", "")
                || "text";

            const label = document.createElement("span");
            label.className = "code-language";
            label.textContent = language;

            const button = document.createElement("button");
            button.type = "button";
            button.className = "code-copy-button";
            button.textContent = "复制";
            button.addEventListener("click", async () => {
                try {
                    await navigator.clipboard.writeText(codeBlock.innerText);
                    button.textContent = "已复制";
                    window.setTimeout(() => {
                        button.textContent = "复制";
                    }, 1400);
                } catch {
                    button.textContent = "复制失败";
                    window.setTimeout(() => {
                        button.textContent = "复制";
                    }, 1400);
                }
            });

            toolbar.append(label, button);
            pre.parentElement.insertBefore(wrapper, pre);
            wrapper.append(toolbar, pre);
        });
    }

    function collectHeadings(contentElement) {
        const headings = [];
        const headingElements = contentElement.querySelectorAll("h2, h3, h4, h5");
        headingElements.forEach((heading) => {
            if (!heading.id) {
                heading.id = slugify(heading.textContent || "section");
            }

            headings.push({
                id: heading.id,
                text: heading.textContent || "",
                level: Number.parseInt(heading.tagName.substring(1), 10)
            });
        });

        return headings;
    }

    function computeActiveHeading(contentElement, scrollElement) {
        const headings = Array.from(contentElement.querySelectorAll("h2, h3, h4, h5"));
        if (!headings.length) {
            return "";
        }

        const containerTop = scrollElement.getBoundingClientRect().top;
        let activeHeading = headings[0];

        headings.forEach((heading) => {
            const distance = heading.getBoundingClientRect().top - containerTop;
            if (distance <= 120) {
                activeHeading = heading;
            }
        });

        return activeHeading.id || "";
    }

    return {
        refreshMarkdownView(contentElement, scrollElement, dotNetReference) {
            if (!contentElement || !scrollElement || !dotNetReference) {
                return [];
            }

            const previousCleanup = markdownState.get(scrollElement);
            if (previousCleanup) {
                previousCleanup();
            }

            decorateCodeBlocks(contentElement);
            const headings = collectHeadings(contentElement);

            const emitActiveHeading = () => {
                const activeHeading = computeActiveHeading(contentElement, scrollElement);
                dotNetReference.invokeMethodAsync("SetActiveHeading", activeHeading);
            };

            scrollElement.addEventListener("scroll", emitActiveHeading, { passive: true });
            window.addEventListener("resize", emitActiveHeading);
            window.requestAnimationFrame(emitActiveHeading);

            markdownState.set(scrollElement, () => {
                scrollElement.removeEventListener("scroll", emitActiveHeading);
                window.removeEventListener("resize", emitActiveHeading);
            });

            return headings;
        },

        scrollToHeading(scrollElement, headingId) {
            if (!scrollElement || !headingId) {
                return;
            }

            const target = scrollElement.querySelector(`#${CSS.escape(headingId)}`);
            if (target) {
                target.scrollIntoView({ behavior: "smooth", block: "start" });
            }
        },

        disposeMarkdownView(scrollElement) {
            const cleanup = markdownState.get(scrollElement);
            if (cleanup) {
                cleanup();
                markdownState.delete(scrollElement);
            }
        }
    };
})();
