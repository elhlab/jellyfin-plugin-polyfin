
# Goal
A proxy which "translates" jellyfin metadata on the fly using a configured metadata provider

## Core Flow
Plugin integrates directly into jellyfin modifying responses

1. intercepted response.
2. determine if request contains metadata to be translated
3. detect user locale
4. fetch metadata language (if locale not default locale)
5. modify response content with the fetched language


### First milestone

- [x] Movie metadata
- [x] jellyfin plugin config (static, eg requiring reload to update)
- [x] Scheduled task that refreshes metadata for every movie x configured locale.

### Future

#### Soon

- [ ] Translated taglines (small)
- [ ] Re-fetch partial metadata after n time (medium)

#### Later

- [ ] Series metadata (large)
- [ ] User selectable locale (large)
- [ ] Default language set to a configured language (e.g. Deutsch) instead of the library's own (medium)
- [ ] Localized posters (large)
- [ ] Explore spoofing default audio track based on user locale (unknown)

#### When I get to them

- [ ] Skip refetching when an item update didn't change its match (medium)
- [ ] Settings page hint: new languages are fetched on the next scheduled refresh (small)
- [ ] Advanced settings: worker count and wait interval (medium)
- [ ] Smoke-test script against the dev Jellyfin (large)
- [ ] Better test coverage (large)

### Known issues and problems

- Search stays in the library's original/stored language, not the
  translated one a viewer sees.
- Sort order and alphabetical browse position don't follow the translated
  title.
- Offline/synced copies bake in whatever language was active at sync time.
- Only title and overview are translated. Genres, studios, tags etc. stay in
  the library's language.
