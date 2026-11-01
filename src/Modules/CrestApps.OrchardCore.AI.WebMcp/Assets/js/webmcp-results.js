// @ts-check
/*
 * Tool results, shared by the WebMCP scripts.
 *
 * A tool answers an MCP-style result: `{ content: [{ type: 'text', text }], isError }`. Browser agents read that shape,
 * and the chat bridge turns it back into the text the AI model reads.
 */

/**
 * Builds a text result.
 *
 * @param {string} text The text the agent reads.
 * @param {boolean} [isError] Whether the result reports a failure.
 */
export function textResult(text, isError) {
  return {
    content: [{ type: 'text', text: text == null ? '' : String(text) }],
    isError: !!isError,
  };
}

/**
 * Whether a tool's answer reports a failure.
 *
 * @param {any} result The answer.
 */
export function isErrorResult(result) {
  return !!(result && typeof result === 'object' && result.isError === true);
}

/**
 * Turns a tool's answer into text: the text parts of a result, a string as it is, and anything else as JSON.
 *
 * @param {any} result The answer.
 */
export function serializeResult(result) {
  if (result == null) {
    return '';
  }

  if (typeof result === 'string') {
    return result;
  }

  if (typeof result === 'object' && Array.isArray(result.content)) {
    return result.content
      .filter(function (part) {
        return part && part.type === 'text';
      })
      .map(function (part) {
        return part.text == null ? '' : String(part.text);
      })
      .join('\n');
  }

  try {
    return JSON.stringify(result);
  } catch (error) {
    return String(result);
  }
}
