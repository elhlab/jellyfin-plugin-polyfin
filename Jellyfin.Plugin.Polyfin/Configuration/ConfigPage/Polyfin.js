// Called by the dashboard once per view, through the page's data-controller.
export default function (page) {
    const pluginId = 'b8c970d4-41d3-4dec-9c37-b37536f36f77';

    // ApiClient and Dashboard are globals provided by the Jellyfin web client.
    const form = page.querySelector('#PolyfinConfigForm');
    const loadProblem = page.querySelector('#LoadProblem');
    const providerTimeoutInput = page.querySelector('#ProviderTimeoutSeconds');
    const languageList = page.querySelector('#LanguageList');
    const languagesProblem = page.querySelector('#LanguagesProblem');
    const configuredUserList = page.querySelector('#ConfiguredUserList');
    const configuredUsersProblem = page.querySelector('#ConfiguredUsersProblem');

    let users = [];

    function field(root, name) {
        return root.querySelector('[data-name="' + name + '"]');
    }

    function cloneTemplate(templateId) {
        return page.querySelector('#' + templateId).content.firstElementChild.cloneNode(true);
    }

    function matchLocaleInputs(languageElement) {
        return [...field(languageElement, 'matchLocales').querySelectorAll('[data-name="matchLocale"]')];
    }

    // crypto.randomUUID needs a secure context, which a plain http:// server isn't.
    function createNewId() {
        if (crypto?.randomUUID) {
            return crypto.randomUUID();
        }

        const bytes = new Uint8Array(16);
        crypto.getRandomValues(bytes);
        return [...bytes].map((byte) => byte.toString(16).padStart(2, '0')).join('');
    }

    // Accepts "de" or "de-DE" in any case, e.g. "DE-de" becomes "de-DE".
    function normalizeTag(value) {
        const match = /^([A-Za-z]{2,3})(?:-([A-Za-z]{2}))?$/.exec(value.trim());
        if (!match) {
            return null;
        }

        const [, language, region] = match;
        return region
            ? language.toLowerCase() + '-' + region.toUpperCase()
            : language.toLowerCase();
    }

    function updateSummary(languageElement) {
        const name = field(languageElement, 'name').value.trim();

        field(languageElement, 'summaryName').textContent = name || 'New language';
        field(languageElement, 'summaryMetadataLocale').textContent = field(languageElement, 'metadataLocale').value.trim();
    }

    function addMatchLocaleRow(languageElement, value) {
        const row = cloneTemplate('MatchLocaleTemplate');
        field(row, 'matchLocale').value = value ?? '';
        field(row, 'removeMatchLocale').addEventListener('click', () => row.remove());

        field(languageElement, 'matchLocales').appendChild(row);
        return row;
    }

    // Fills in the metadata locale as the first pattern, but only while every pattern is empty.
    function suggestMatchLocale(languageElement) {
        const suggestion = normalizeTag(field(languageElement, 'metadataLocale').value);
        const inputs = matchLocaleInputs(languageElement);

        if (!suggestion || inputs.some((input) => input.value.trim())) {
            return;
        }

        if (inputs.length) {
            inputs[0].value = suggestion;
        } else {
            addMatchLocaleRow(languageElement, suggestion);
        }
    }

    function getUserName(userId) {
        return users.find((user) => user.Id === userId)?.Name ?? userId;
    }

    function sortBy(items, getter) {
        return [...items].sort((a, b) => getter(a).localeCompare(getter(b)));
    }

    function fillSelect(select, options, placeholder, selectedValue) {
        select.innerHTML = '';
        select.appendChild(new Option(placeholder, ''));
        for (const { value, label } of options) {
            select.appendChild(new Option(label, value));
        }

        // A value no longer on offer leaves the select empty, so saving asks for a new pick.
        select.value = selectedValue ?? '';
    }

    function fillUserSelect(select, selectedUserId) {
        const options = users.map((user) => ({ value: user.Id, label: user.Name }));

        // Keep a deleted user selectable, so their saved row still shows and can be removed.
        if (selectedUserId && !users.some((user) => user.Id === selectedUserId)) {
            options.push({ value: selectedUserId, label: 'Unknown user (' + selectedUserId.slice(0, 8) + '…)' });
        }

        fillSelect(select, options, 'Choose a user', selectedUserId);
    }

    function fillLanguageSelect(select, selectedLanguageId) {
        const options = [...languageList.children].map((languageElement) => ({
            value: field(languageElement, 'id').value,
            label: field(languageElement, 'name').value.trim() || 'Unnamed language'
        }));

        fillSelect(select, options, 'Choose a language', selectedLanguageId);
    }

    function refreshLanguageSelects() {
        for (const select of configuredUserList.querySelectorAll('[data-name="languageId"]')) {
            fillLanguageSelect(select, select.value);
        }
    }

    function addConfiguredUser(configuredUser) {
        const row = cloneTemplate('ConfiguredUserTemplate');

        fillUserSelect(field(row, 'userId'), configuredUser.UserId);
        fillLanguageSelect(field(row, 'languageId'), configuredUser.LanguageId);
        field(row, 'removeConfiguredUser').addEventListener('click', () => row.remove());

        configuredUserList.appendChild(row);
        return row;
    }

    function addLanguage(language, expanded) {
        const languageElement = cloneTemplate('LanguageTemplate');
        const nameInput = field(languageElement, 'name');
        const metadataLocaleInput = field(languageElement, 'metadataLocale');

        nameInput.value = language.Name ?? '';
        metadataLocaleInput.value = language.MetadataLocale ?? '';
        field(languageElement, 'id').value = language.Id ?? createNewId();
        for (const matchLocale of language.MatchLocales ?? []) {
            addMatchLocaleRow(languageElement, matchLocale);
        }

        nameInput.addEventListener('input', () => {
            updateSummary(languageElement);
            refreshLanguageSelects();
        });
        metadataLocaleInput.addEventListener('input', () => updateSummary(languageElement));
        metadataLocaleInput.addEventListener('change', () => suggestMatchLocale(languageElement));

        field(languageElement, 'addMatchLocale').addEventListener('click', () => {
            field(addMatchLocaleRow(languageElement, ''), 'matchLocale').focus();
        });
        field(languageElement, 'removeLanguage').addEventListener('click', () => {
            const name = nameInput.value.trim() || 'this language';
            if (confirm('Remove ' + name + '? Clients relying on this language lose that reference, even if you add it back.')) {
                languageElement.remove();
                refreshLanguageSelects();
            }
        });

        languageElement.open = Boolean(expanded);
        updateSummary(languageElement);
        languageList.appendChild(languageElement);
        refreshLanguageSelects();
        return languageElement;
    }

    // Reads the form as plain values. Each value keeps its input, so a problem can point at it.
    function readForm() {
        const languages = [...languageList.children].map((languageElement) => {
            const nameInput = field(languageElement, 'name');
            const metadataLocaleInput = field(languageElement, 'metadataLocale');

            return {
                id: field(languageElement, 'id').value,
                name: nameInput.value.trim(),
                nameInput,
                metadataLocaleText: metadataLocaleInput.value.trim(),
                metadataLocale: normalizeTag(metadataLocaleInput.value),
                metadataLocaleInput,
                matchLocales: matchLocaleInputs(languageElement)
                    .filter((input) => input.value.trim())
                    .map((input) => ({ text: input.value.trim(), tag: normalizeTag(input.value), input })),
                problemElement: field(languageElement, 'problem')
            };
        });

        const configuredUsers = [...configuredUserList.children].map((row) => {
            const userSelect = field(row, 'userId');
            const languageSelect = field(row, 'languageId');

            return {
                userId: userSelect.value,
                userName: userSelect.options[userSelect.selectedIndex]?.text ?? '',
                userSelect,
                languageId: languageSelect.value,
                languageSelect
            };
        });

        return { languages, configuredUsers };
    }

    function findProblem({ languages, configuredUsers }) {
        const notATag = (text) => '"' + text + '" is not a language tag. Use "de" or "de-AT".';

        for (const language of languages) {
            const shownIn = language.problemElement;

            if (!language.name) {
                return { input: language.nameInput, shownIn, message: 'Give this language a name.' };
            }

            if (!language.metadataLocaleText) {
                return { input: language.metadataLocaleInput, shownIn, message: 'Give this language a metadata locale, e.g. "de".' };
            }

            if (!language.metadataLocale) {
                return { input: language.metadataLocaleInput, shownIn, message: notATag(language.metadataLocaleText) };
            }

            for (const matchLocale of language.matchLocales) {
                if (!matchLocale.tag) {
                    return { input: matchLocale.input, shownIn, message: notATag(matchLocale.text) };
                }
            }
        }

        // "de" and "de-AT" in different languages is fine; the same pattern in two is ambiguous.
        const languageByTag = new Map();
        for (const language of languages) {
            for (const matchLocale of language.matchLocales) {
                const other = languageByTag.get(matchLocale.tag);
                if (other && other !== language) {
                    return {
                        input: matchLocale.input,
                        shownIn: languagesProblem,
                        message: '"' + matchLocale.tag + '" is listed under both ' + other.name + ' and ' + language.name + '. Leave it on one of them.'
                    };
                }

                languageByTag.set(matchLocale.tag, language);
            }
        }

        const seenUserIds = new Set();
        for (const configuredUser of configuredUsers) {
            const shownIn = configuredUsersProblem;

            if (!configuredUser.userId) {
                return { input: configuredUser.userSelect, shownIn, message: 'Pick a user on every row, or remove the row.' };
            }

            // Empty when the row's language has been removed.
            if (!configuredUser.languageId) {
                return {
                    input: configuredUser.languageSelect,
                    shownIn,
                    message: languages.length
                        ? 'Pick a language for ' + configuredUser.userName + '.'
                        : 'Remove ' + configuredUser.userName + ', or add back a language to pick for them.'
                };
            }

            if (seenUserIds.has(configuredUser.userId)) {
                return { input: configuredUser.userSelect, shownIn, message: configuredUser.userName + ' is listed twice; a user can only be pinned to one language.' };
            }

            seenUserIds.add(configuredUser.userId);
        }

        return null;
    }

    // Clears every problem message, then shows the given one, if any.
    function showProblem(problem) {
        const problemElements = [
            languagesProblem,
            configuredUsersProblem,
            ...[...languageList.children].map((languageElement) => field(languageElement, 'problem'))
        ];
        for (const element of problemElements) {
            element.textContent = '';
        }

        if (!problem) {
            return;
        }

        problem.shownIn.textContent = problem.message;

        const languageElement = problem.input.closest('details');
        if (languageElement) {
            languageElement.open = true;
        }

        problem.input.focus();
    }

    async function loadConfig() {
        Dashboard.showLoadingMsg();

        try {
            const [config, loadedUsers] = await Promise.all([
                ApiClient.getPluginConfiguration(pluginId),
                ApiClient.getUsers()
            ]);

            users = loadedUsers ?? [];
            render(config);
            form.hidden = false;
            loadProblem.hidden = true;
        } catch {
            // A half-filled form could be saved over the stored settings, so show none.
            form.hidden = true;
            loadProblem.hidden = false;
        } finally {
            Dashboard.hideLoadingMsg();
        }
    }

    function render(config) {
        providerTimeoutInput.value = config.ProviderTimeoutSeconds;

        // Cleared first, since adding a language refreshes the pickers in the user rows.
        languageList.innerHTML = '';
        configuredUserList.innerHTML = '';

        // Only sorted on load, so nothing moves while the admin is editing.
        for (const language of sortBy(config.Languages ?? [], (language) => language.Name)) {
            addLanguage(language, false);
        }

        for (const configuredUser of sortBy(config.Users ?? [], (configuredUser) => getUserName(configuredUser.UserId))) {
            addConfiguredUser(configuredUser);
        }
    }

    page.addEventListener('viewshow', loadConfig);
    page.querySelector('#RetryLoad').addEventListener('click', loadConfig);

    page.querySelector('#AddLanguage').addEventListener('click', () => {
        const languageElement = addLanguage({}, true);
        addMatchLocaleRow(languageElement, '');
        field(languageElement, 'name').focus();
    });

    page.querySelector('#AddConfiguredUser').addEventListener('click', () => addConfiguredUser({}));

    form.addEventListener('submit', async (e) => {
        e.preventDefault();

        const { languages, configuredUsers } = readForm();
        const problem = findProblem({ languages, configuredUsers });
        showProblem(problem);
        if (problem) {
            return;
        }

        Dashboard.showLoadingMsg();

        try {
            const config = await ApiClient.getPluginConfiguration(pluginId);
            config.ProviderTimeoutSeconds = parseInt(providerTimeoutInput.value, 10);
            config.Languages = languages.map((language) => ({
                Id: language.id,
                Name: language.name,
                MetadataLocale: language.metadataLocale,
                MatchLocales: language.matchLocales.map((matchLocale) => matchLocale.tag)
            }));
            config.Users = configuredUsers.map((configuredUser) => ({
                UserId: configuredUser.userId,
                LanguageId: configuredUser.languageId
            }));

            const result = await ApiClient.updatePluginConfiguration(pluginId, config);
            Dashboard.processPluginConfigurationUpdateResult(result);
        } catch {
            Dashboard.hideLoadingMsg();
            Dashboard.alert({ title: 'Settings not saved', message: 'The server could not save the settings. Your changes are still on the page, so you can try again.' });
            return;
        }

        // Re-read so the page shows what was actually stored, sorted.
        await loadConfig();
    });
}
