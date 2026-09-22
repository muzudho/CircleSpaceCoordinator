{
  "models": [
    {
      "id": "events",
      "type": "viewport",
      "children": [
        {
          "id": "regular",
          "type": "container",
          "children": [
            {
              "id": "title",
              "type": "container"
            },
            {
              "id": "description",
              "type": "container"
            },
            {
              "id": "body",
              "type": "container",
              "children": [
                {
                  "id": "list",
                  "type": "container"
                },
                {
                  "id": "actions",
                  "type": "container",
                  "children": [
                    {
                      "id": "open",
                      "type": "button"
                    },
                    {
                      "id": "create",
                      "type": "button"
                    },
                    {
                      "id": "edit",
                      "type": "button"
                    },
                    {
                      "id": "register",
                      "type": "button"
                    },
                    {
                      "id": "duplicate",
                      "type": "button"
                    },
                    {
                      "id": "confidential",
                      "type": "button"
                    },
                    {
                      "id": "up",
                      "type": "button"
                    },
                    {
                      "id": "down",
                      "type": "button"
                    },
                    {
                      "id": "remove",
                      "type": "button"
                    },
                    {
                      "id": "exit",
                      "type": "button"
                    }
                  ]
                }
              ]
            },
            {
              "id": "reload",
              "type": "button"
            },
            {
              "id": "error",
              "type": "container"
            },
            {
              "id": "footer",
              "type": "container"
            }
          ]
        },
        {
          "id": "compact",
          "type": "container",
          "children": [
            {
              "id": "title",
              "type": "container"
            },
            {
              "id": "description",
              "type": "container"
            },
            {
              "id": "body",
              "type": "container",
              "children": [
                {
                  "id": "list",
                  "type": "container"
                },
                {
                  "id": "actions",
                  "type": "container",
                  "children": [
                    {
                      "id": "open",
                      "type": "button"
                    },
                    {
                      "id": "create",
                      "type": "button"
                    },
                    {
                      "id": "edit",
                      "type": "button"
                    },
                    {
                      "id": "register",
                      "type": "button"
                    },
                    {
                      "id": "duplicate",
                      "type": "button"
                    },
                    {
                      "id": "confidential",
                      "type": "button"
                    },
                    {
                      "id": "up",
                      "type": "button"
                    },
                    {
                      "id": "down",
                      "type": "button"
                    },
                    {
                      "id": "remove",
                      "type": "button"
                    },
                    {
                      "id": "exit",
                      "type": "button"
                    }
                  ]
                }
              ]
            },
            {
              "id": "reload",
              "type": "button"
            },
            {
              "id": "error",
              "type": "container"
            },
            {
              "id": "footer",
              "type": "container"
            }
          ]
        },
        {
          "id": "rowTemplate",
          "type": "container",
          "children": [
            {
              "id": "item",
              "type": "container",
              "children": [
                {
                  "id": "title",
                  "type": "container"
                },
                {
                  "id": "detail",
                  "type": "container"
                }
              ]
            }
          ]
        }
      ]
    }
  ],
  "layouts": [
    {
      "id": "regularPanel",
      "type": "box-layout",
      "padding": {
        "top": "22px",
        "right": "24px",
        "bottom": "20px",
        "left": "24px"
      },
      "children": [
        {
          "id": "page",
          "type": "grid-layout",
          "row-definitions": [
            "42px",
            "4px",
            "28px",
            "12px",
            "1rate",
            "12px",
            "38px",
            "6px",
            "24px",
            "28px"
          ],
          "column-definitions": [
            "1rate"
          ],
          "children": [
            {
              "id": "body",
              "type": "grid-layout",
              "row-definitions": [
                "1rate"
              ],
              "column-definitions": [
                "1rate",
                "24px",
                "224px"
              ],
              "row": 4,
              "col": 0,
              "children": [
                {
                  "id": "actions",
                  "type": "grid-layout",
                  "row-definitions": [
                    "38px",
                    "6px",
                    "38px",
                    "6px",
                    "38px",
                    "6px",
                    "38px",
                    "6px",
                    "38px",
                    "6px",
                    "38px",
                    "6px",
                    "38px",
                    "6px",
                    "38px",
                    "6px",
                    "38px",
                    "6px",
                    "38px",
                    "6px",
                    "1rate"
                  ],
                  "column-definitions": [
                    "1rate"
                  ],
                  "row": 0,
                  "col": 2,
                  "cells": [
                    {
                      "row": 0,
                      "col": 0
                    },
                    {
                      "row": 2,
                      "col": 0
                    },
                    {
                      "row": 4,
                      "col": 0
                    },
                    {
                      "row": 6,
                      "col": 0
                    },
                    {
                      "row": 8,
                      "col": 0
                    },
                    {
                      "row": 10,
                      "col": 0
                    },
                    {
                      "row": 12,
                      "col": 0
                    },
                    {
                      "row": 14,
                      "col": 0
                    },
                    {
                      "row": 16,
                      "col": 0
                    },
                    {
                      "row": 18,
                      "col": 0
                    }
                  ]
                }
              ],
              "cells": [
                {
                  "row": 0,
                  "col": 0
                }
              ]
            }
          ],
          "cells": [
            {
              "row": 0,
              "col": 0
            },
            {
              "row": 2,
              "col": 0
            },
            {
              "row": 6,
              "col": 0
            },
            {
              "row": 8,
              "col": 0
            },
            {
              "row": 9,
              "col": 0
            }
          ]
        }
      ]
    },
    {
      "id": "compactPanel",
      "type": "box-layout",
      "padding": {
        "top": "22px",
        "right": "24px",
        "bottom": "20px",
        "left": "24px"
      },
      "children": [
        {
          "id": "page",
          "type": "grid-layout",
          "row-definitions": [
            "42px",
            "4px",
            "28px",
            "12px",
            "1rate",
            "12px",
            "38px",
            "6px",
            "24px",
            "28px"
          ],
          "column-definitions": [
            "1rate"
          ],
          "children": [
            {
              "id": "body",
              "type": "grid-layout",
              "row-definitions": [
                "1rate"
              ],
              "column-definitions": [
                "1rate",
                "24px",
                "344px"
              ],
              "row": 4,
              "col": 0,
              "children": [
                {
                  "id": "actions",
                  "type": "grid-layout",
                  "row-definitions": [
                    "38px",
                    "6px",
                    "38px",
                    "6px",
                    "38px",
                    "6px",
                    "38px",
                    "6px",
                    "38px",
                    "6px",
                    "1rate"
                  ],
                  "column-definitions": [
                    "1rate",
                    "12px",
                    "1rate"
                  ],
                  "row": 0,
                  "col": 2,
                  "cells": [
                    {
                      "row": 0,
                      "col": 0
                    },
                    {
                      "row": 0,
                      "col": 2
                    },
                    {
                      "row": 2,
                      "col": 0
                    },
                    {
                      "row": 2,
                      "col": 2
                    },
                    {
                      "row": 4,
                      "col": 0
                    },
                    {
                      "row": 4,
                      "col": 2
                    },
                    {
                      "row": 6,
                      "col": 0
                    },
                    {
                      "row": 6,
                      "col": 2
                    },
                    {
                      "row": 8,
                      "col": 0
                    },
                    {
                      "row": 8,
                      "col": 2
                    }
                  ]
                }
              ],
              "cells": [
                {
                  "row": 0,
                  "col": 0
                }
              ]
            }
          ],
          "cells": [
            {
              "row": 0,
              "col": 0
            },
            {
              "row": 2,
              "col": 0
            },
            {
              "row": 6,
              "col": 0
            },
            {
              "row": 8,
              "col": 0
            },
            {
              "row": 9,
              "col": 0
            }
          ]
        }
      ]
    },
    {
      "id": "eventRow",
      "type": "grid-layout",
      "row-definitions": [
        "58px",
        "6px"
      ],
      "column-definitions": [
        "1rate"
      ],
      "children": [
        {
          "id": "padding",
          "type": "box-layout",
          "padding": {
            "top": "5px",
            "right": "10px",
            "bottom": "7px",
            "left": "10px"
          },
          "row": 0,
          "col": 0,
          "children": [
            {
              "id": "text",
              "type": "grid-layout",
              "row-definitions": [
                "24px",
                "2px",
                "20px"
              ],
              "column-definitions": [
                "1rate"
              ],
              "cells": [
                {
                  "row": 0,
                  "col": 0
                },
                {
                  "row": 2,
                  "col": 0
                }
              ]
            }
          ]
        }
      ]
    }
  ],
  "bindings": [
    {
      "layout": "/compactPanel",
      "model": "/events/compact"
    },
    {
      "layout": "/regularPanel",
      "model": "/events/regular"
    },
    {
      "layout": "/regularPanel/page",
      "parentModel": "/events/regular",
      "childrenModel": [
        {
          "model": "title",
          "cell": {
            "row": 1,
            "col": 1
          }
        },
        {
          "model": "description",
          "cell": {
            "row": 3,
            "col": 1
          }
        },
        {
          "model": "reload",
          "cell": {
            "row": 7,
            "col": 1
          }
        },
        {
          "model": "error",
          "cell": {
            "row": 9,
            "col": 1
          }
        },
        {
          "model": "footer",
          "cell": {
            "row": 10,
            "col": 1
          }
        }
      ]
    },
    {
      "layout": "/regularPanel/page/body",
      "parentModel": "/events/regular",
      "childrenModel": [
        {
          "model": "body/list",
          "cell": {
            "row": 1,
            "col": 1
          }
        }
      ]
    },
    {
      "layout": "/regularPanel/page/body/actions",
      "parentModel": "/events/regular",
      "childrenModel": [
        {
          "model": "body/actions/open",
          "cell": {
            "row": 1,
            "col": 1
          }
        },
        {
          "model": "body/actions/create",
          "cell": {
            "row": 3,
            "col": 1
          }
        },
        {
          "model": "body/actions/edit",
          "cell": {
            "row": 5,
            "col": 1
          }
        },
        {
          "model": "body/actions/register",
          "cell": {
            "row": 7,
            "col": 1
          }
        },
        {
          "model": "body/actions/duplicate",
          "cell": {
            "row": 9,
            "col": 1
          }
        },
        {
          "model": "body/actions/confidential",
          "cell": {
            "row": 11,
            "col": 1
          }
        },
        {
          "model": "body/actions/up",
          "cell": {
            "row": 13,
            "col": 1
          }
        },
        {
          "model": "body/actions/down",
          "cell": {
            "row": 15,
            "col": 1
          }
        },
        {
          "model": "body/actions/remove",
          "cell": {
            "row": 17,
            "col": 1
          }
        },
        {
          "model": "body/actions/exit",
          "cell": {
            "row": 19,
            "col": 1
          }
        }
      ]
    },
    {
      "layout": "/compactPanel/page",
      "parentModel": "/events/compact",
      "childrenModel": [
        {
          "model": "title",
          "cell": {
            "row": 1,
            "col": 1
          }
        },
        {
          "model": "description",
          "cell": {
            "row": 3,
            "col": 1
          }
        },
        {
          "model": "reload",
          "cell": {
            "row": 7,
            "col": 1
          }
        },
        {
          "model": "error",
          "cell": {
            "row": 9,
            "col": 1
          }
        },
        {
          "model": "footer",
          "cell": {
            "row": 10,
            "col": 1
          }
        }
      ]
    },
    {
      "layout": "/compactPanel/page/body",
      "parentModel": "/events/compact",
      "childrenModel": [
        {
          "model": "body/list",
          "cell": {
            "row": 1,
            "col": 1
          }
        }
      ]
    },
    {
      "layout": "/compactPanel/page/body/actions",
      "parentModel": "/events/compact",
      "childrenModel": [
        {
          "model": "body/actions/open",
          "cell": {
            "row": 1,
            "col": 1
          }
        },
        {
          "model": "body/actions/create",
          "cell": {
            "row": 1,
            "col": 3
          }
        },
        {
          "model": "body/actions/edit",
          "cell": {
            "row": 3,
            "col": 1
          }
        },
        {
          "model": "body/actions/register",
          "cell": {
            "row": 3,
            "col": 3
          }
        },
        {
          "model": "body/actions/duplicate",
          "cell": {
            "row": 5,
            "col": 1
          }
        },
        {
          "model": "body/actions/confidential",
          "cell": {
            "row": 5,
            "col": 3
          }
        },
        {
          "model": "body/actions/up",
          "cell": {
            "row": 7,
            "col": 1
          }
        },
        {
          "model": "body/actions/down",
          "cell": {
            "row": 7,
            "col": 3
          }
        },
        {
          "model": "body/actions/remove",
          "cell": {
            "row": 9,
            "col": 1
          }
        },
        {
          "model": "body/actions/exit",
          "cell": {
            "row": 9,
            "col": 3
          }
        }
      ]
    },
    {
      "layout": "/eventRow/padding/text",
      "parentModel": "/events/rowTemplate",
      "childrenModel": [
        {
          "model": "item/title",
          "cell": {
            "row": 1,
            "col": 1
          }
        },
        {
          "model": "item/detail",
          "cell": {
            "row": 3,
            "col": 1
          }
        }
      ]
    }
  ]
}
