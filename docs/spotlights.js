/* Rotate the documentation spotlights at midnight UTC. */
(() => {
    const day = Math.floor(Date.now() / 86_400_000);
    const tableHeader = '| Project | Credits | How it uses CsoundUnity | Links | Year |';
    const linkPattern = /\[([^\]]+)\]\((https:\/\/[^\s)]+)\)/g;
    const catalogs = new Map();

    function loadCatalog(file) {
        if (!catalogs.has(file)) {
            catalogs.set(file, fetch(new URL(file, document.baseURI)).then(response => {
                if (!response.ok) throw new Error('Catalog unavailable');
                return response.text();
            }).catch(error => {
                catalogs.delete(file);
                throw error;
            }));
        }
        return catalogs.get(file);
    }

    function projects(markdown) {
        const lines = markdown.split(/\r?\n/);
        const header = lines.indexOf(tableHeader);
        if (header < 0) return [];
        const entries = [];
        for (const line of lines.slice(header + 2)) {
            if (!line.startsWith('|')) break;
            const [title, credits, description, links] = line.slice(1, -1)
                .split(/(?<!\\)\|/).map(cell => cell.trim().replaceAll('\\|', '|'));
            if (title && credits && description && links) entries.push({ title, credits, description, links });
        }
        return entries.sort((a, b) => a.title.toLowerCase().localeCompare(b.title.toLowerCase(), 'en'));
    }

    function papers(markdown) {
        return [...markdown.matchAll(/^- (.+?), \*\[([^\]]+)\]\((https:\/\/[^\s)]+)\)\*, (.+)$/gm)]
            .map(([, credits, title, url, details]) => ({
                title, credits, description: details.split(/\.(?=\s|$)/, 1)[0] + '.',
                links: '[Read the reference](' + url + ')',
            }));
    }

    async function render(id, file, parse, offset) {
        const slot = document.getElementById(id);
        if (!slot || slot.dataset.loaded) return;
        slot.dataset.loaded = 'true';
        try {
            const entries = parse(await loadCatalog(file));
            if (!entries.length) return;
            const index = ((day + offset) % entries.length + entries.length) % entries.length;
            const { title, credits, description, links } = entries[index];
            const label = document.createElement('strong');
            label.textContent = title;
            const body = document.createElement('p');
            body.append(label, ' by ' + credits + '. ' + description.replace(/[*`]/g, ''));

            const resources = document.createElement('p');
            for (const [, name, url] of links.matchAll(linkPattern)) {
                if (resources.childNodes.length) resources.append(' · ');
                const link = document.createElement('a');
                link.href = url;
                link.textContent = name;
                resources.append(link);
            }
            slot.replaceChildren(body, resources);
        } catch {
            slot.textContent = 'The spotlight is unavailable. Browse the full list below.';
            delete slot.dataset.loaded;
        }
    }

    window.csoundUnitySpotlights = function (hook) {
        hook.doneEach(() => {
            render('daily-project', 'PROJECTS.md', projects, 0);
            render('daily-paper', 'PAPERS.md', papers, 8);
        });
    };
})();
