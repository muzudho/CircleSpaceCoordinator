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
      }
    },
    {
      "id": "regularPage",
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
      ]
    },
    {
      "id": "regularBody",
      "type": "grid-layout",
      "row-definitions": [
        "1rate"
      ],
      "column-definitions": [
        "1rate",
        "24px",
        "224px"
      ]
    },
    {
      "id": "regularActions",
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
      }
    },
    {
      "id": "compactPage",
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
      ]
    },
    {
      "id": "compactBody",
      "type": "grid-layout",
      "row-definitions": [
        "1rate"
      ],
      "column-definitions": [
        "1rate",
        "24px",
        "344px"
      ]
    },
    {
      "id": "compactActions",
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
      ]
    },
    {
      "id": "eventRowPadding",
      "type": "box-layout",
      "padding": {
        "top": "5px",
        "right": "10px",
        "bottom": "7px",
        "left": "10px"
      }
    },
    {
      "id": "eventRowText",
      "type": "grid-layout",
      "row-definitions": [
        "24px",
        "2px",
        "20px"
      ],
      "column-definitions": [
        "1rate"
      ]
    }
  ],
  "bindings": [
    {
      "layout": "regularPanel",
      "model": "/events/regular"
    },
    {
      "layout": "regularPage",
      "parentModel": "/events/regular",
      "childrenModel": [
        {
          "model": "title",
          "row": 0,
          "column": 0
        },
        {
          "model": "description",
          "row": 2,
          "column": 0
        },
        {
          "model": "body",
          "row": 4,
          "column": 0
        },
        {
          "model": "reload",
          "row": 6,
          "column": 0
        },
        {
          "model": "error",
          "row": 8,
          "column": 0
        },
        {
          "model": "footer",
          "row": 9,
          "column": 0
        }
      ]
    },
    {
      "layout": "regularBody",
      "parentModel": "/events/regular/body",
      "childrenModel": [
        {
          "model": "list",
          "row": 0,
          "column": 0
        },
        {
          "model": "actions",
          "row": 0,
          "column": 2
        }
      ]
    },
    {
      "layout": "regularActions",
      "parentModel": "/events/regular/body/actions",
      "childrenModel": [
        {
          "model": "open",
          "row": 0,
          "column": 0
        },
        {
          "model": "create",
          "row": 2,
          "column": 0
        },
        {
          "model": "edit",
          "row": 4,
          "column": 0
        },
        {
          "model": "register",
          "row": 6,
          "column": 0
        },
        {
          "model": "duplicate",
          "row": 8,
          "column": 0
        },
        {
          "model": "confidential",
          "row": 10,
          "column": 0
        },
        {
          "model": "up",
          "row": 12,
          "column": 0
        },
        {
          "model": "down",
          "row": 14,
          "column": 0
        },
        {
          "model": "remove",
          "row": 16,
          "column": 0
        },
        {
          "model": "exit",
          "row": 18,
          "column": 0
        }
      ]
    },
    {
      "layout": "compactPanel",
      "model": "/events/compact"
    },
    {
      "layout": "compactPage",
      "parentModel": "/events/compact",
      "childrenModel": [
        {
          "model": "title",
          "row": 0,
          "column": 0
        },
        {
          "model": "description",
          "row": 2,
          "column": 0
        },
        {
          "model": "body",
          "row": 4,
          "column": 0
        },
        {
          "model": "reload",
          "row": 6,
          "column": 0
        },
        {
          "model": "error",
          "row": 8,
          "column": 0
        },
        {
          "model": "footer",
          "row": 9,
          "column": 0
        }
      ]
    },
    {
      "layout": "compactBody",
      "parentModel": "/events/compact/body",
      "childrenModel": [
        {
          "model": "list",
          "row": 0,
          "column": 0
        },
        {
          "model": "actions",
          "row": 0,
          "column": 2
        }
      ]
    },
    {
      "layout": "compactActions",
      "parentModel": "/events/compact/body/actions",
      "childrenModel": [
        {
          "model": "open",
          "row": 0,
          "column": 0
        },
        {
          "model": "create",
          "row": 0,
          "column": 2
        },
        {
          "model": "edit",
          "row": 2,
          "column": 0
        },
        {
          "model": "register",
          "row": 2,
          "column": 2
        },
        {
          "model": "duplicate",
          "row": 4,
          "column": 0
        },
        {
          "model": "confidential",
          "row": 4,
          "column": 2
        },
        {
          "model": "up",
          "row": 6,
          "column": 0
        },
        {
          "model": "down",
          "row": 6,
          "column": 2
        },
        {
          "model": "remove",
          "row": 8,
          "column": 0
        },
        {
          "model": "exit",
          "row": 8,
          "column": 2
        }
      ]
    },
    {
      "layout": "eventRow",
      "parentModel": "/events/rowTemplate",
      "childrenModel": [
        {
          "model": "item",
          "row": 0,
          "column": 0
        }
      ]
    },
    {
      "layout": "eventRowPadding",
      "model": "/events/rowTemplate/item"
    },
    {
      "layout": "eventRowText",
      "parentModel": "/events/rowTemplate/item",
      "childrenModel": [
        {
          "model": "title",
          "row": 0,
          "column": 0
        },
        {
          "model": "detail",
          "row": 2,
          "column": 0
        }
      ]
    }
  ]
}
