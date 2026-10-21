# Fixture pages

Synthetic pages that copy the **structure** of real sites (their JSON-LD
shapes, Open Graph tags and quirks) with invented content. Real pages are
not committed: this repo is public and their text and images belong to
someone else. Real snapshots belong in the data pool (Milestone 2), kept
privately.

| File | Modelled on | What it exercises |
|---|---|---|
| `ticketing-aggregate-offer.html` | Eventbrite (structure checked 21 Oct 2026) | Four JSON-LD blocks (WebPage, BusinessEvent, FAQPage, BreadcrumbList); AggregateOffer with string prices including "0.0"; streetAddress repeating the town |
| `music-venue-graph.html` | Venue sites using `@graph` | MusicEvent inside `@graph`, `@type` as an array, no offset on the start time |
| `wordpress-events-calendar.html` | WordPress "The Events Calendar" plugin | Array of Events, HTML entities in names, escaped HTML in descriptions, date-only end |
| `venue-whats-on-list.html` | A venue's "what's on" page | Several events, one already past |
| `restaurant-local-business.html` | Restaurant sites | Restaurant JSON-LD (address) and no Event |
| `blog-no-structured-data.html` | A blog/listicle post | OG tags and body text only: the LLM has to do everything |
| `javascript-shell.html` | Single-page apps | Almost nothing until JavaScript runs |
| `broken-json-ld.html` | Hand-edited sites | One malformed JSON-LD block next to a valid one |
