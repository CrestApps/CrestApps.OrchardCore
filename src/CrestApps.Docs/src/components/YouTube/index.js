import React from 'react';
import videos from '@site/src/data/videos.json';
import styles from './styles.module.css';

/**
 * A video from the CrestApps YouTube channel, embedded through YouTube's privacy-enhanced domain, which sets no
 * cookies until the reader plays it. Videos are kept on YouTube rather than in the repository, so cloning it does
 * not download every video.
 *
 * Usage: <YouTube video="ai-profile-builder" title="Video overview of the AI Profile Builder" />
 * `video` names a video of the CrestApps.Videos repository; its YouTube id is read from src/data/videos.json, which
 * that repository's publish workflow updates through a pull request whenever a video, or a new version of it, is
 * published. Until then the component renders nothing, so a page can embed a video before it is published.
 *
 * `id` takes a YouTube id directly. Add start and end, in seconds, to play one chapter of a longer video:
 * <YouTube video="ai-profile-builder" title="..." start={99} end={146} />
 */
export default function YouTube({video, id, title, start, end}) {
  const videoId = id || videos[video];

  if (!videoId) {
    return null;
  }

  const parameters = new URLSearchParams({rel: '0'});

  if (start) {
    parameters.set('start', String(start));
  }

  if (end) {
    parameters.set('end', String(end));
  }

  return (
    <div className={styles.video}>
      <iframe
        src={`https://www.youtube-nocookie.com/embed/${videoId}?${parameters}`}
        title={title}
        loading="lazy"
        allow="accelerometer; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share"
        referrerPolicy="strict-origin-when-cross-origin"
        allowFullScreen
      />
    </div>
  );
}
