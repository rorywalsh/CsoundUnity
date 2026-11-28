/* Build every page's section tree with the same heading compiler as Docsify. */
(() => {
    window.csoundUnitySidebar = function (hook, vm) {
        const pages = new Map();
        const expanded = new Map();
        let lastPath;
        let nextId = 0;

        function directLists(item) {
            return Array.from(item.children).filter(child => child.tagName === 'UL');
        }

        function setExpanded(item, open) {
            const button = item.querySelector(':scope > button, :scope > p > button');
            if (!button) return;
            const key = button.dataset.sidebarKey;
            expanded.set(key, open);
            button.setAttribute('aria-expanded', String(open));
            if (!button.classList.contains('sidebar-group')) {
                button.setAttribute('aria-label', (open ? 'Collapse ' : 'Expand ') + button.dataset.label);
            }
            directLists(item).forEach(list => { list.hidden = !open; });
        }

        function decorate(nav) {
            nav.querySelectorAll('li').forEach(item => {
                const lists = directLists(item);
                if (!lists.length) return;
                const link = item.querySelector(':scope > a');
                const title = item.querySelector(':scope > p');
                if (!link && !title) return;
                let button = item.querySelector(':scope > button, :scope > p > button');
                const label = link ? link.textContent.trim() : title.textContent.trim();
                const key = link ? link.getAttribute('href') : 'group:' + label;
                if (!button) {
                    button = document.createElement('button');
                    button.type = 'button';
                    button.className = link ? 'sidebar-expand' : 'sidebar-group';
                    button.dataset.sidebarKey = key;
                    button.dataset.label = label;
                    if (link) {
                        item.classList.add('sidebar-branch');
                        item.prepend(button);
                    } else {
                        button.textContent = label;
                        button.setAttribute('aria-label', label);
                        title.replaceChildren(button);
                    }
                }
                lists.forEach(list => {
                    if (!list.id) list.id = 'sidebar-tree-' + (++nextId);
                    list.dataset.sidebarTree = 'true';
                });
                button.setAttribute('aria-controls', lists.map(list => list.id).join(' '));
                setExpanded(item, expanded.get(key) || false);
            });
        }

        function reveal(item) {
            while (item && item.classList && !item.classList.contains('sidebar-nav')) {
                if (item.tagName === 'LI') setExpanded(item, true);
                item = item.parentElement;
            }
        }

        function select(nav, revealCurrent, wanted = new URL(location.href).hash) {
            const path = wanted.split('?')[0];
            let selected;
            nav.querySelectorAll('a').forEach(link => {
                link.removeAttribute('aria-current');
                link.parentElement.classList.remove('active');
                if (link.hash === wanted) selected = link;
            });
            if (!selected) {
                selected = Array.from(nav.querySelectorAll('a')).find(link => link.hash === path);
            }
            if (selected) {
                selected.setAttribute('aria-current', wanted.includes('?id=') ? 'location' : 'page');
                selected.parentElement.classList.add('active');
                if (revealCurrent) reveal(selected.parentElement);
            }
        }

        function compileSections(markdown, path) {
            const router = Object.create(vm.router);
            router.getCurrentPath = () => path;
            // Each background compilation uses its own renderer.
            const config = Object.assign({}, vm.config, {
                markdown: (marked, renderer) => text => marked(text, { renderer }),
            });
            const compiler = new window.DocsifyCompiler(config, router);
            compiler.compile(markdown);
            const headings = compiler.toc;
            if (!headings.length || headings[0].ignoreAllSubs) return [];
            return headings.slice(1).filter(heading => !heading.ignoreSubHeading);
        }

        function loadPage(path) {
            if (!pages.has(path)) {
                const request = fetch(vm.router.getFile(path)).then(response => {
                    if (!response.ok) throw new Error('HTTP ' + response.status);
                    return response.text();
                }).then(markdown => compileSections(markdown, path)).catch(error => {
                    pages.delete(path);
                    throw error;
                });
                pages.set(path, request);
            }
            return pages.get(path);
        }

        function sectionTree(headings) {
            const root = document.createElement('ul');
            root.className = 'sidebar-sections';
            const stack = [{ level: 0, list: root }];
            for (const heading of headings) {
                while (stack.length > 1 && stack[stack.length - 1].level >= heading.level) stack.pop();
                const parent = stack[stack.length - 1];
                const item = document.createElement('li');
                const link = document.createElement('a');
                const label = document.createElement('template');
                label.innerHTML = heading.title;
                link.textContent = label.content.textContent.trim();
                link.href = heading.slug;
                link.className = 'section-link';
                item.append(link);
                parent.list.append(item);
                const children = document.createElement('ul');
                item.append(children);
                stack.push({ level: heading.level, list: children });
            }
            root.querySelectorAll('ul:empty').forEach(list => list.remove());
            return root;
        }

        hook.doneEach(function () {
            const nav = document.querySelector('.sidebar-nav');
            if (!nav) return;
            const path = vm.route.path;
            const changedPage = path !== lastPath;
            lastPath = path;
            const pageLinks = Array.from(nav.querySelectorAll('a')).filter(link => {
                const href = link.getAttribute('href');
                return href && href.startsWith('#/') && href !== '#/' && !href.includes('?');
            });
            for (const link of pageLinks) {
                const item = link.parentElement;
                const placeholder = document.createElement('ul');
                placeholder.className = 'sidebar-sections';
                placeholder.setAttribute('aria-busy', 'true');
                const loading = document.createElement('li');
                loading.textContent = 'Loading sections…';
                placeholder.append(loading);
                item.append(placeholder);
                const pagePath = link.hash.slice(1);
                loadPage(pagePath).then(headings => {
                    if (!item.isConnected) return;
                    if (headings.length) placeholder.replaceWith(sectionTree(headings));
                    else placeholder.remove();
                    if (!directLists(item).length) {
                        item.querySelector(':scope > button')?.remove();
                        item.classList.remove('sidebar-branch');
                    }
                    decorate(nav);
                    select(nav, pagePath === path);
                }).catch(error => {
                    if (!item.isConnected) return;
                    loading.textContent = 'Sections unavailable. Open the page to read it.';
                    placeholder.removeAttribute('aria-busy');
                    console.warn('[Sidebar] Cannot load sections for ' + pagePath, error);
                });
            }
            decorate(nav);
            select(nav, changedPage || location.hash.includes('?id='));
            const wanted = new URLSearchParams(location.hash.split('?')[1] || '').get('id');
            if (wanted) {
                const scroll = () => document.getElementById(wanted)?.scrollIntoView();
                if (document.readyState === 'complete') scroll();
                else window.addEventListener('load', scroll, { once: true });
            }
        });

        hook.ready(function () {
            document.addEventListener('click', event => {
                const button = event.target.closest('button[data-sidebar-key]');
                if (!button) return;
                event.preventDefault();
                event.stopPropagation();
                const item = button.closest('li');
                setExpanded(item, button.getAttribute('aria-expanded') !== 'true');
            }, true);
            window.addEventListener('hashchange', () => {
                const nav = document.querySelector('.sidebar-nav');
                if (nav) select(nav, true);
            });
            let scrollPending = false;
            document.addEventListener('scroll', () => {
                if (scrollPending) return;
                scrollPending = true;
                requestAnimationFrame(() => {
                    scrollPending = false;
                    const nav = document.querySelector('.sidebar-nav');
                    if (!nav) return;
                    const headings = document.querySelectorAll('.markdown-section h1, .markdown-section h2, .markdown-section h3, .markdown-section h4, .markdown-section h5, .markdown-section h6');
                    let current;
                    for (const heading of headings) {
                        if (heading.getBoundingClientRect().top <= 80) current = heading.querySelector('.anchor');
                    }
                    if (current) select(nav, false, current.hash);
                });
            }, { passive: true });
        });
    };
})();
