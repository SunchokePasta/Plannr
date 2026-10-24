// PLANNED (M4): Mapping for Captures, with the (Status, CreatedAt) index the claim searches by.
// Connects to: backend/src/Plannr.Data/Entities/Capture.cs
// TODO(H7.2): the index on (Status, CreatedAt), so finding the oldest waiting capture reads a few
//   index entries instead of the whole table; and one on (Status, AnnouncedAt) for the announcer.
// Tracked in docs/structure.json; remove this header and the entry when it's built.
