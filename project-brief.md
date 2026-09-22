
# Goal
A proxy which "translates" jellyfin metadata on the fly using a configured metadata provider

## Core Flow
Plugin integrates directly into jellyfin modifying responses

1. intercepted response.
2. determine if request contains metadata to be translated
3. detect user locale
4. fetch metadata language (if locale not default locale)
5. modify response content with the fetched language


### Scope

- Movie metadata
- jellyfin plugin config (static, eg requiring reload to update)
- Scheduled task that resolves and caches metadata in the background for
  every movie x admin-configured locale.

### Future

- Series metadata
- User selectable locale
- Default language set to a configured language (e.g. Deutsch) instead of the library's own.
- Better test coverage
- Explore spoofing default audio track based on user locale
- Testing against the docker-compose Jellyfin instance

### Known issues and problems

- Search stays in the library's original/stored language, not the
  translated one a viewer sees.
- Images/posters aren't localized.
- Sort order and alphabetical browse position don't follow the translated
  title.
- Offline/synced copies bake in whatever language was active at sync time.
- Only title and overview are translated. Genres, studios, tags etc. stay in
  the library's language.
