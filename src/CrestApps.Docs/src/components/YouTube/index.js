import React from 'react';
import styles from './styles.module.css';

/**
 * A video from the CrestApps YouTube channel, embedded through YouTube's privacy-enhanced domain, which sets no
 * cookies until the reader plays it. Videos are kept on YouTube rather than in the repository, so cloning it does
 * not download every video.
 *
 * Usage: <YouTube id="UOKvEY-kID4" title="Video overview of the AI Profile Builder" />
 * Add start and end, in seconds, to play one chapter of a longer video: <YouTube id="..." title="..." start={99} end={146} />
 */
export default function YouTube({id, title, start, end}) {
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
        src={`https://www.youtube-nocookie.com/embed/${id}?${parameters}`}
        title={title}
        loading="lazy"
        allow="accelerometer; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share"
        referrerPolicy="strict-origin-when-cross-origin"
        allowFullScreen
      />
    </div>
  );
}
